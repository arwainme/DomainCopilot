using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Services;

public sealed class RetrievalService : IRetrievalService
{
    private const double MinimumSemanticThreshold = 0.35;
    private const int MaximumLexicalScore = 8;

    private readonly IDocumentRepository _documentRepository;
    private readonly ILlmProvider _llmProvider;

    private IReadOnlyList<StoredChunk> _chunks =
        Array.Empty<StoredChunk>();

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _loaded;

    private static readonly HashSet<string> StopWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "what",
            "is",
            "are",
            "the",
            "a",
            "an",
            "for",
            "of",
            "to",
            "and",
            "or",
            "in",
            "on",
            "how",
            "can",
            "do",
            "does",
            "did",
            "i",
            "my",
            "me",
            "this",
            "that",
            "with",
            "from",
            "government",
            "service",
            "services",
            "request",
            "requested"
        };

    private static readonly Dictionary<string, string[]> QuerySynonyms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cost"] = ["fee", "fees", "charge", "charges", "payment", "price"],
            ["price"] = ["fee", "fees", "cost", "charge", "payment"],
            ["pay"] = ["fee", "fees", "payment", "charge", "cost"],
            ["payment"] = ["fee", "fees", "payment", "charge", "cost"],

            ["long"] = ["processing", "timeline", "time", "duration", "deadline"],
            ["duration"] = ["processing", "timeline", "time", "deadline"],
            ["days"] = ["processing", "timeline", "time", "duration"],

            ["steps"] = ["procedure", "process", "application", "apply", "submit"],
            ["apply"] = ["application", "procedure", "process", "submit"],
            ["application"] = ["apply", "procedure", "process", "submit"],

            ["paperwork"] = ["documents", "document", "papers", "form"],
            ["papers"] = ["documents", "document", "paperwork", "form"],

            ["id"] = ["identification", "identity", "document"],

            ["eligible"] = ["eligibility", "qualify", "qualification", "requirements"],
            ["qualify"] = ["eligible", "eligibility", "qualification", "requirements"]
        };

    public RetrievalService(
        IDocumentRepository documentRepository,
        ILlmProvider llmProvider)
    {
        _documentRepository = documentRepository;
        _llmProvider = llmProvider;
    }

    public async Task<IReadOnlyList<EvidenceChunk>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<EvidenceChunk>();
        }

        await EnsureLoadedAsync(cancellationToken);

        var terms = BuildQueryTerms(query);

        if (terms.Count == 0)
        {
            return Array.Empty<EvidenceChunk>();
        }

        IReadOnlyList<float>? queryEmbedding = null;

        try
        {
            queryEmbedding =
                await _llmProvider.GenerateEmbeddingAsync(
                    query,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Retrieval] Query embedding unavailable. " +
                $"Falling back to lexical retrieval: {ex.Message}");
        }

        var lexicalCandidates = _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                LexicalScore = CalculateLexicalScore(
                    terms,
                    chunk.Evidence)
            })
            .Where(x => x.LexicalScore >= 1)
            .OrderByDescending(x => x.LexicalScore)
            .ThenBy(x => x.Chunk.Evidence.ChunkId)
            .Take(40)
            .ToList();

        var ranked =
            new List<(EvidenceChunk Chunk, double Score)>();

        foreach (var candidate in lexicalCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lexicalScore = candidate.LexicalScore;
            var semanticScore = 0.0;

            if (queryEmbedding is not null &&
                queryEmbedding.Count > 0 &&
                candidate.Chunk.Embedding is not null &&
                candidate.Chunk.Embedding.Count > 0)
            {
                semanticScore =
                    CosineSimilarity(
                        queryEmbedding,
                        candidate.Chunk.Embedding);
            }

            var normalizedLexical =
                Math.Min(
                    lexicalScore / (double)MaximumLexicalScore,
                    1.0);

            var hasSemantic =
                queryEmbedding is not null &&
                queryEmbedding.Count > 0 &&
                candidate.Chunk.Embedding is not null &&
                candidate.Chunk.Embedding.Count > 0;

            var finalScore =
                hasSemantic
                    ? (normalizedLexical * 0.45) +
                      (semanticScore * 0.55)
                    : normalizedLexical;

            if (lexicalScore >= 1 ||
                semanticScore >= MinimumSemanticThreshold)
            {
                ranked.Add(
                    (candidate.Chunk.Evidence, finalScore));
            }
        }

        var results =
            ranked
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Chunk.ChunkId)
                .Take(Math.Max(1, topK))
                .Select(x => x.Chunk)
                .ToList();

        Console.WriteLine(
            $"[Retrieval] Query='{query}' " +
            $"Terms={string.Join(",", terms)} " +
            $"PersistedChunks={_chunks.Count} " +
            $"Results={results.Count}");

        return results;
    }

    private async Task EnsureLoadedAsync(
        CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);

        try
        {
            if (_loaded)
            {
                return;
            }

            // IMPORTANT:
            // Query the same DbContext sequentially.
            var documents =
                await _documentRepository.GetAllDocumentsAsync(
                    cancellationToken);

            var chunks =
                await _documentRepository.GetAllChunksAsync(
                    cancellationToken);

            var documentTitles =
                documents.ToDictionary(
                    x => x.Id,
                    x => x.Title);

            var storedChunks =
                new List<StoredChunk>();

            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(chunk.Content))
                {
                    continue;
                }

                var title =
                    documentTitles.TryGetValue(
                        chunk.DocumentId,
                        out var documentTitle)
                        ? documentTitle
                        : chunk.DocumentId.ToString();

                var embedding =
                    ParseEmbedding(chunk.Embedding);

                var evidence =
                    new EvidenceChunk(
                        chunk.Id,
                        chunk.DocumentId,
                        title,
                        chunk.Content,
                        chunk.PageNumber?.ToString());

                storedChunks.Add(
                    new StoredChunk(
                        evidence,
                        embedding));
            }

            _chunks = storedChunks;
            _loaded = true;

            Console.WriteLine(
                $"[Retrieval] Loaded {_chunks.Count} persisted chunks from SQL.");
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private static HashSet<string> BuildQueryTerms(
        string query)
    {
        var terms =
            query
                .Split(
                    [
                        ' ',
                        ',',
                        '.',
                        '?',
                        '!',
                        ':',
                        ';',
                        '-',
                        '/',
                        '(',
                        ')'
                    ],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .Where(x => x.Length >= 2)
                .Where(x => !StopWords.Contains(x))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var expandedTerms =
            new HashSet<string>(
                terms,
                StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms)
        {
            if (!QuerySynonyms.TryGetValue(
                    term,
                    out var synonyms))
            {
                continue;
            }

            foreach (var synonym in synonyms)
            {
                expandedTerms.Add(
                    synonym.ToLowerInvariant());
            }
        }

        return expandedTerms;
    }

    private static int CalculateLexicalScore(
        IReadOnlySet<string> terms,
        EvidenceChunk chunk)
    {
        var content =
            chunk.Content ?? string.Empty;

        var title =
            chunk.DocumentTitle ?? string.Empty;

        var score = 0;

        foreach (var term in terms)
        {
            if (content.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }

            if (title.Contains(
                    term,
                    StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
        }

        return score;
    }

    private static IReadOnlyList<float>? ParseEmbedding(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<float[]>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double CosineSimilarity(
        IReadOnlyList<float> a,
        IReadOnlyList<float> b)
    {
        if (a.Count == 0 ||
            b.Count == 0)
        {
            return 0;
        }

        var length =
            Math.Min(a.Count, b.Count);

        double dot = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (var i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 ||
            magnitudeB == 0)
        {
            return 0;
        }

        return dot /
               (Math.Sqrt(magnitudeA) *
                Math.Sqrt(magnitudeB));
    }

    private sealed record StoredChunk(
        EvidenceChunk Evidence,
        IReadOnlyList<float>? Embedding);
}

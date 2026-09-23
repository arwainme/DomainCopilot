using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Application.Abstractions;
namespace DomainCopilot.Application.Services;

public sealed class RetrievalService : IRetrievalService
{
    private const double StrongSemanticThreshold = 0.50;
    private const double MinimumSemanticThreshold = 0.35;
    private const int MaximumLexicalScore = 8;

    private readonly IReadOnlyCollection<EvidenceChunk> _chunks;
    private readonly ILlmProvider _llmProvider;
    private readonly DocumentIngestionService _ingestionService;
    private readonly Dictionary<string, IReadOnlyList<float>> _embeddingCache =
        new(StringComparer.Ordinal);

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
            ["cost"] =
            [
                "fee",
                "fees",
                "charge",
                "charges",
                "payment",
                "price"
            ],

            ["price"] =
            [
                "fee",
                "fees",
                "cost",
                "charge",
                "payment"
            ],

            ["pay"] =
            [
                "fee",
                "fees",
                "payment",
                "charge",
                "cost"
            ],

            ["payment"] =
            [
                "fee",
                "fees",
                "payment",
                "charge",
                "cost"
            ],

            ["long"] =
            [
                "processing",
                "timeline",
                "time",
                "duration",
                "deadline"
            ],

            ["duration"] =
            [
                "processing",
                "timeline",
                "time",
                "deadline"
            ],

            ["days"] =
            [
                "processing",
                "timeline",
                "time",
                "duration"
            ],

            ["steps"] =
            [
                "procedure",
                "process",
                "application",
                "apply",
                "submit"
            ],

            ["apply"] =
            [
                "application",
                "procedure",
                "process",
                "submit"
            ],

            ["application"] =
            [
                "apply",
                "procedure",
                "process",
                "submit"
            ],

            ["paperwork"] =
            [
                "documents",
                "document",
                "papers",
                "form"
            ],

            ["papers"] =
            [
                "documents",
                "document",
                "paperwork",
                "form"
            ],

            ["id"] =
            [
                "identification",
                "identity",
                "document"
            ],

            ["eligible"] =
            [
                "eligibility",
                "qualify",
                "qualification",
                "requirements"
            ],

            ["qualify"] =
            [
                "eligible",
                "eligibility",
                "qualification",
                "requirements"
            ]
        };

    public RetrievalService(
        ILlmProvider llmProvider,
        DocumentIngestionService ingestionService)
    {
        _llmProvider = llmProvider;
        _ingestionService = ingestionService;

        var projectRoot = FindProjectRoot();

        var documentsPath = Path.Combine(
            projectRoot,
            "data",
            "documents");

        _chunks = LoadDocuments(documentsPath);

        Console.WriteLine(
            $"[Retrieval] Loaded {_chunks.Count} chunks from {documentsPath}");
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

        var terms = BuildQueryTerms(query);

        if (terms.Count == 0)
        {
            return Array.Empty<EvidenceChunk>();
        }

        // Phase 1: lexical candidate generation.
        // This avoids requesting embeddings for every chunk in the corpus.
        var lexicalCandidates = _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                LexicalScore = CalculateLexicalScore(terms, chunk)
            })
            .Where(x => x.LexicalScore >= 1)
            .OrderByDescending(x => x.LexicalScore)
            .ThenBy(x => x.Chunk.ChunkId)
            .Take(40)
            .ToList();

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

        var ranked = new List<(EvidenceChunk Chunk, double Score)>();

        foreach (var candidate in lexicalCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lexicalScore = candidate.LexicalScore;
            var semanticScore = 0.0;

            if (queryEmbedding is not null &&
                queryEmbedding.Count > 0)
            {
                try
                {
                    var chunkEmbedding =
                        await GetEmbeddingAsync(
                            candidate.Chunk.Content,
                            cancellationToken);

                    semanticScore =
                        CosineSimilarity(
                            queryEmbedding,
                            chunkEmbedding);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Retrieval] Chunk embedding unavailable. " +
                        $"Using lexical score for this chunk: {ex.Message}");
                }
            }

            var normalizedLexical =
                Math.Min(
                    lexicalScore /
                    MaximumLexicalScore,
                    1.0);

            var finalScore =
                queryEmbedding is not null &&
                queryEmbedding.Count > 0
                    ? (normalizedLexical * 0.45) +
                      (semanticScore * 0.55)
                    : normalizedLexical;

            // Keep lexical evidence, or strong semantic evidence.
            if (lexicalScore >= 1 ||
                semanticScore >= MinimumSemanticThreshold)
            {
                ranked.Add(
                    (candidate.Chunk, finalScore));
            }
        }

        var results = ranked
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.ChunkId)
            .Take(Math.Max(1, topK))
            .Select(x => x.Chunk)
            .ToList();

        Console.WriteLine(
            $"[Retrieval] Query='{query}' " +
            $"Terms={string.Join(",", terms)} " +
            $"LexicalCandidates={lexicalCandidates.Count} " +
            $"Results={results.Count}");

        return results;
    }
    private static HashSet<string> BuildQueryTerms(
        string query)
    {
        var terms = query
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

    private async Task<IReadOnlyList<float>> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken)
    {
        if (_embeddingCache.TryGetValue(
                text,
                out var cached))
        {
            return cached;
        }

        var embedding =
            await _llmProvider.GenerateEmbeddingAsync(
                text,
                cancellationToken);

        _embeddingCache[text] = embedding;

        return embedding;
    }

    private static double CosineSimilarity(
        IReadOnlyList<float> a,
        IReadOnlyList<float> b)
    {
        if (a.Count == 0 || b.Count == 0)
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

    private IReadOnlyCollection<EvidenceChunk> LoadDocuments(
        string documentsPath)
    {
        if (!Directory.Exists(documentsPath))
        {
            return Array.Empty<EvidenceChunk>();
        }

        var files = Directory
            .GetFiles(
                documentsPath,
                "*.txt",
                SearchOption.AllDirectories)
            .Concat(
                Directory.GetFiles(
                    documentsPath,
                    "*.pdf",
                    SearchOption.AllDirectories))
            .ToList();

        var chunks = new List<EvidenceChunk>();

        foreach (var filePath in files)
        {
            var fileChunks =
                _ingestionService.ExtractAndChunk(filePath);

            chunks.AddRange(fileChunks);
        }

        return chunks;
    }
    private static string FindProjectRoot()
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var documentsDirectory =
                Path.Combine(
                    directory.FullName,
                    "data",
                    "documents");

            if (Directory.Exists(
                    documentsDirectory))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the project data/documents directory.");
    }
}

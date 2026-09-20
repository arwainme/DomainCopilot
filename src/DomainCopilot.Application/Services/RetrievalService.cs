using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Services;

public sealed class RetrievalService : IRetrievalService
{
    private readonly IReadOnlyCollection<EvidenceChunk> _chunks;
    private readonly ILlmProvider _llmProvider;

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
            "i",
            "my",
            "me",
            "this",
            "that",
            "with",
            "from",
            "government"
        };

    public RetrievalService(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;

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

        var terms = query
            .Split(
                [' ', ',', '.', '?', '!', ':', ';', '-', '/', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Where(x => x.Length >= 3)
            .Where(x => !StopWords.Contains(x))
            .ToHashSet();

        if (terms.Count == 0)
        {
            return Array.Empty<EvidenceChunk>();
        }

        var lexicalScores = _chunks
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = terms.Count(term =>
                    chunk.Content.Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase) ||
                    chunk.DocumentTitle.Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase))
            })
            .ToDictionary(
                x => x.Chunk.ChunkId,
                x => x.Score);

        IReadOnlyList<float>? queryEmbedding = null;

        try
        {
            queryEmbedding = await _llmProvider.GenerateEmbeddingAsync(
                query,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[Retrieval] Embedding retrieval unavailable: {ex.Message}");
        }

        var ranked = new List<(EvidenceChunk Chunk, double Score)>();

        foreach (var chunk in _chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lexicalScore =
                lexicalScores.TryGetValue(
                    chunk.ChunkId,
                    out var value)
                    ? value
                    : 0;

            double semanticScore = 0;

            if (queryEmbedding is not null &&
                queryEmbedding.Count > 0)
            {
                try
                {
                    var chunkEmbedding =
                        await GetEmbeddingAsync(
                            chunk.Content,
                            cancellationToken);

                    semanticScore = CosineSimilarity(
                        queryEmbedding,
                        chunkEmbedding);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Retrieval] Chunk embedding failed: {ex.Message}");
                }
            }

            /*
             * Relevance gate:
             *
             * A chunk must either:
             * 1. Match at least two meaningful query terms, or
             * 2. Have a strong semantic similarity score.
             *
             * This prevents generic words such as "procedure" or
             * "government" from being enough to produce evidence.
             */
            var hasStrongLexicalMatch = lexicalScore >= 2;
            var hasStrongSemanticMatch = semanticScore >= 0.65;

            if (!hasStrongLexicalMatch &&
                !hasStrongSemanticMatch)
            {
                continue;
            }

            var normalizedLexical =
                Math.Min(lexicalScore / 5.0, 1.0);

            var finalScore =
                (normalizedLexical * 0.4) +
                (semanticScore * 0.6);

            ranked.Add((chunk, finalScore));
        }

        return ranked
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.ChunkId)
            .Take(Math.Max(1, topK))
            .Select(x => x.Chunk)
            .ToList();
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

        var length = Math.Min(a.Count, b.Count);

        double dot = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (var i = 0; i < length; i++)
        {
            dot += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
        {
            return 0;
        }

        return dot /
               (Math.Sqrt(magnitudeA) *
                Math.Sqrt(magnitudeB));
    }

    private static IReadOnlyCollection<EvidenceChunk> LoadDocuments(
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
            var ingestionService = new DocumentIngestionService();

            var fileChunks = ingestionService
                .IngestAsync(filePath)
                .GetAwaiter()
                .GetResult();

            chunks.AddRange(fileChunks);
        }

        return chunks;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(
            AppContext.BaseDirectory);

        while (directory is not null)
        {
            var documentsDirectory = Path.Combine(
                directory.FullName,
                "data",
                "documents");

            if (Directory.Exists(documentsDirectory))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the project data/documents directory.");
    }
}
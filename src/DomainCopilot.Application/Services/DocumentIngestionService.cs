using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Domain.Entities;
using UglyToad.PdfPig;

namespace DomainCopilot.Application.Services;

public sealed class DocumentIngestionService
{
    private const int ChunkSize = 500;

    private readonly IDocumentRepository _documentRepository;
    private readonly ILlmProvider _llmProvider;

    public DocumentIngestionService(
        IDocumentRepository documentRepository,
        ILlmProvider llmProvider)
    {
        _documentRepository = documentRepository;
        _llmProvider = llmProvider;
    }

    public async Task<IReadOnlyList<EvidenceChunk>> IngestAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Document was not found.",
                filePath);
        }

        var extension =
            Path.GetExtension(filePath).ToLowerInvariant();

        if (extension is not ".txt" and not ".pdf")
        {
            throw new NotSupportedException(
                $"Unsupported document format: {extension}");
        }

        var contentHash = ComputeFileHash(filePath);

        var documentId =
            CreateDeterministicGuid(contentHash);

        var existing =
            await _documentRepository.GetByIdAsync(
                documentId,
                cancellationToken);

        // Same content was already ingested.
        if (existing is not null &&
            existing.Status == Domain.Enums.DocumentStatus.Completed)
        {
            return await ToEvidenceChunksAsync(
                existing,
                cancellationToken);
        }

        var text = extension switch
        {
            ".txt" => await File.ReadAllTextAsync(
                filePath,
                cancellationToken),

            ".pdf" => ExtractPdfText(filePath),

            _ => throw new NotSupportedException()
        };

        text = CleanText(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException(
                "The document contains no readable text.");
        }

        var document =
            existing ??
            new Document(
                Path.GetFileNameWithoutExtension(filePath),
                filePath,
                contentHash,
                documentId);

        document.MarkAsProcessing();

        if (existing is null)
        {
            await _documentRepository.AddAsync(
                document,
                cancellationToken);
        }

        await _documentRepository.SaveChangesAsync(
            cancellationToken);

        try
        {
            var evidenceChunks =
                CreateChunks(
                    text,
                    documentId,
                    document.Title,
                    contentHash,
                    extension);

            var persistedChunks =
                new List<DocumentChunk>();

            foreach (var evidence in evidenceChunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<float> embedding;

                try
                {
                    embedding =
                        await _llmProvider.GenerateEmbeddingAsync(
                            evidence.Content,
                            cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Embedding failed for chunk {evidence.ChunkId}.",
                        ex);
                }

                var chunk =
                    new DocumentChunk(
                        evidence.DocumentId,
                        evidence.Content,
                        persistedChunks.Count,
                        pageNumber: ParsePageNumber(
                            evidence.PageNumber),
                        id: evidence.ChunkId);

                chunk.SetEmbedding(
                    JsonSerializer.Serialize(embedding));

                persistedChunks.Add(chunk);
            }

            await _documentRepository.AddChunksAsync(
                persistedChunks,
                cancellationToken);

            document.MarkAsCompleted();

            await _documentRepository.SaveChangesAsync(
                cancellationToken);

            return evidenceChunks;
        }
        catch (Exception ex)
        {
            document.MarkAsFailed(
                ex.Message);

            await _documentRepository.SaveChangesAsync(
                CancellationToken.None);

            throw;
        }
    }

    private async Task<IReadOnlyList<EvidenceChunk>>
        ToEvidenceChunksAsync(
            Document document,
            CancellationToken cancellationToken)
    {
        var chunks =
            await _documentRepository
                .GetChunksByDocumentIdAsync(
                    document.Id,
                    cancellationToken);

        return chunks
            .OrderBy(x => x.ChunkIndex)
            .Select(x =>
                new EvidenceChunk(
                    x.Id,
                    document.Id,
                    document.Title,
                    x.Content,
                    x.PageNumber?.ToString()))
            .ToList();
    }

    private static List<EvidenceChunk> CreateChunks(
        string text,
        Guid documentId,
        string documentTitle,
        string contentHash,
        string extension)
    {
        var chunks = new List<EvidenceChunk>();

        for (var offset = 0;
             offset < text.Length;
             offset += ChunkSize)
        {
            var length =
                Math.Min(
                    ChunkSize,
                    text.Length - offset);

            var content =
                text.Substring(
                    offset,
                    length);

            var chunkId =
                CreateDeterministicGuid(
                    $"{contentHash}:{offset}");

            chunks.Add(
                new EvidenceChunk(
                    chunkId,
                    documentId,
                    documentTitle,
                    content,
                    $"offset:{offset};format:{extension};hash:{contentHash}"));
        }

        return chunks;
    }

    private static int? ParsePageNumber(
        string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        var marker = "page:";

        var index =
            metadata.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return null;
        }

        var value =
            metadata[(index + marker.Length)..]
                .Split(';')[0];

        return int.TryParse(
            value,
            out var page)
            ? page
            : null;
    }

    private static string ExtractPdfText(
        string filePath)
    {
        var builder = new StringBuilder();

        using var document =
            PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string CleanText(
        string text)
    {
        return string.Join(
            Environment.NewLine,
            text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line =>
                    !string.IsNullOrWhiteSpace(line)));
    }

    private static string ComputeFileHash(
        string filePath)
    {
        using var stream =
            File.OpenRead(filePath);

        return Convert.ToHexString(
            SHA256.HashData(stream));
    }

    private static Guid CreateDeterministicGuid(
        string value)
    {
        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(value));

        return new Guid(
            hash.Take(16).ToArray());
    }
}
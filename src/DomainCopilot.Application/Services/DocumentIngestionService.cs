using System.Security.Cryptography;
using System.Text;
using UglyToad.PdfPig;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Services;

public sealed class DocumentIngestionService
{
    private const int ChunkSize = 500;

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

        var text = extension switch
        {
            ".txt" => await File.ReadAllTextAsync(
                filePath,
                cancellationToken),

            ".pdf" => ExtractPdfText(filePath),

            _ => throw new NotSupportedException(
                $"Unsupported document format: {extension}")
        };

        text = CleanText(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<EvidenceChunk>();
        }

        // Deterministic document identity.
        // The same file content produces the same ID,
        // which makes repeated ingestion idempotent.
        var contentHash = ComputeFileHash(filePath);

        var documentId =
            CreateDeterministicGuid(contentHash);

        var documentTitle =
            Path.GetFileNameWithoutExtension(filePath);

        var chunks = new List<EvidenceChunk>();

        for (var offset = 0;
             offset < text.Length;
             offset += ChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var length = Math.Min(
                ChunkSize,
                text.Length - offset);

            var content = text.Substring(
                offset,
                length);

            var chunkId = CreateDeterministicGuid(
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
                .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string ComputeFileHash(
        string filePath)
    {
        using var stream =
            File.OpenRead(filePath);

        var hash =
            SHA256.HashData(stream);

        return Convert.ToHexString(hash);
    }

    private static Guid CreateDeterministicGuid(
        string value)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return new Guid(
            hash.Take(16).ToArray());
    }
}
using DomainCopilot.Domain.Entities;

namespace DomainCopilot.Application.Abstractions;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Document>> GetAllDocumentsAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default);

    Task AddChunksAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetAllChunksAsync(
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}

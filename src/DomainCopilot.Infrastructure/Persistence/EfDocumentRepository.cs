using DomainCopilot.Application.Abstractions;
using DomainCopilot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Persistence;

public sealed class EfDocumentRepository : IDocumentRepository
{
    private readonly DomainCopilotDbContext _db;

    public EfDocumentRepository(DomainCopilotDbContext db)
    {
        _db = db;
    }

    public Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => _db.Documents
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Document>> GetAllDocumentsAsync(
        CancellationToken cancellationToken = default)
        => await _db.Documents
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
        => await _db.DocumentChunks
            .Where(x => x.DocumentId == documentId)
            .OrderBy(x => x.ChunkIndex)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentChunk>> GetAllChunksAsync(
        CancellationToken cancellationToken = default)
        => await _db.DocumentChunks
            .OrderBy(x => x.DocumentId)
            .ThenBy(x => x.ChunkIndex)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default)
        => await _db.Documents.AddAsync(document, cancellationToken);

    public async Task AddChunksAsync(
        IEnumerable<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
        => await _db.DocumentChunks.AddRangeAsync(chunks, cancellationToken);

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}

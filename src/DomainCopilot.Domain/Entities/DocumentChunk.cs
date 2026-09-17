using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; private set; }

    public Guid DocumentId { get; private set; }

    public string Content { get; private set; }

    public int ChunkIndex { get; private set; }

    public string? Section { get; private set; }

    public int? PageNumber { get; private set; }

    public string? Clause { get; private set; }

    public string? Embedding { get; private set; }

    private DocumentChunk()
    {
        Content = string.Empty;
    }

    public DocumentChunk(
        Guid documentId,
        string content,
        int chunkIndex,
        string? section = null,
        int? pageNumber = null,
        string? clause = null)
    {
        Id = Guid.NewGuid();
        DocumentId = documentId;
        Content = content;
        ChunkIndex = chunkIndex;
        Section = section;
        PageNumber = pageNumber;
        Clause = clause;
    }
}
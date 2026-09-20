using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Application.DTOs;

public sealed record EvidenceChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    string Content,
    string? PageNumber = null);
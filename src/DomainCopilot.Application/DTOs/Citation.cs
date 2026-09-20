using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Application.DTOs;

public sealed record Citation(
    Guid DocumentId,
    Guid ChunkId,
    string DocumentTitle,
    string? PageNumber);
using System;
using System.Collections.Generic;
using System.Text;
namespace DomainCopilot.Application.DTOs;

public sealed record ProcedureResult(
    bool IsSupported,
    IReadOnlyCollection<string> Steps,
    IReadOnlyCollection<EvidenceChunk> Evidence);
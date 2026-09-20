using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Application.DTOs;

public sealed record EligibilityResult(
    bool IsSupported,
    string Explanation,
    IReadOnlyCollection<EvidenceChunk> Evidence);
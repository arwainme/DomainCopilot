using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public interface IProcedureResolver
{
    Task<AgentResult<ProcedureResult>> ExecuteAsync(
        CitizenQuery query,
        EligibilityResult eligibility,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default);
}
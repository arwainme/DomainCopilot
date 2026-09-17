using System;
using System.Collections.Generic;
using System.Text;

using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public interface IResponseDrafter
{
    Task<AgentResult<DraftResponse>> ExecuteAsync(
        CitizenQuery query,
        EligibilityResult eligibility,
        ProcedureResult procedure,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default);
}
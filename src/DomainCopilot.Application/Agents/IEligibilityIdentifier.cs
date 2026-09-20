using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public interface IEligibilityIdentifier
{
    Task<AgentResult<EligibilityResult>> ExecuteAsync(
        CitizenQuery query,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default);
}
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public sealed class EligibilityIdentifier : IEligibilityIdentifier
{
    public Task<AgentResult<EligibilityResult>> ExecuteAsync(
        CitizenQuery query,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (evidence.Count == 0)
        {
            return Task.FromResult(
                new AgentResult<EligibilityResult>(
                    false,
                    null,
                    "Insufficient evidence to determine eligibility."));
        }

        var meaningfulEvidence = evidence
            .Where(chunk =>
                !string.IsNullOrWhiteSpace(chunk.Content))
            .ToList();

        if (meaningfulEvidence.Count == 0)
        {
            return Task.FromResult(
                new AgentResult<EligibilityResult>(
                    false,
                    null,
                    "Insufficient evidence to determine eligibility."));
        }

        var result = new EligibilityResult(
            IsSupported: true,
            Explanation:
                "The available government guidance supports reviewing the citizen's eligibility and required documents. Specific eligibility cannot be confirmed when the citizen's circumstances are incomplete.",
            Evidence: meaningfulEvidence);

        return Task.FromResult(
            new AgentResult<EligibilityResult>(
                true,
                result,
                null));
    }
}
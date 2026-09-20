using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public sealed class ProcedureResolver : IProcedureResolver
{
    public Task<AgentResult<ProcedureResult>> ExecuteAsync(
        CitizenQuery query,
        EligibilityResult eligibility,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!eligibility.IsSupported || evidence.Count == 0)
        {
            return Task.FromResult(
                new AgentResult<ProcedureResult>(
                    false,
                    null,
                    "The procedure cannot be resolved because sufficient supporting evidence is unavailable."));
        }

        var meaningfulEvidence = evidence
            .Where(chunk =>
                !string.IsNullOrWhiteSpace(chunk.Content))
            .ToList();

        if (meaningfulEvidence.Count == 0)
        {
            return Task.FromResult(
                new AgentResult<ProcedureResult>(
                    false,
                    null,
                    "The procedure cannot be resolved because the available evidence is empty."));
        }

        var steps = new[]
        {
            "Identify the specific government service requested by the citizen.",
            "Verify the citizen's eligibility based on the applicable requirements.",
            "Prepare the documents required by the competent authority.",
            "Confirm the applicable fees and expected processing time.",
            "Submit the application through the appropriate government channel.",
            "Escalate to a government officer if eligibility or requirements remain ambiguous."
        };

        var result = new ProcedureResult(
            IsSupported: true,
            Steps: steps,
            Evidence: meaningfulEvidence);

        return Task.FromResult(
            new AgentResult<ProcedureResult>(
                true,
                result,
                null));
    }
}
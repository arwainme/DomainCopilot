using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Agents;

public sealed class ResponseDrafter : IResponseDrafter
{
    public Task<AgentResult<DraftResponse>> ExecuteAsync(
        CitizenQuery query,
        EligibilityResult eligibility,
        ProcedureResult procedure,
        IReadOnlyCollection<EvidenceChunk> evidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!eligibility.IsSupported)
        {
            return Task.FromResult(
                new AgentResult<DraftResponse>(
                    false,
                    null,
                    "A response cannot be drafted because eligibility could not be supported by the available evidence."));
        }

        if (!procedure.IsSupported)
        {
            return Task.FromResult(
                new AgentResult<DraftResponse>(
                    false,
                    null,
                    "A response cannot be drafted because the procedure could not be supported by the available evidence."));
        }

        var meaningfulEvidence = evidence
            .Where(chunk =>
                !string.IsNullOrWhiteSpace(chunk.Content))
            .ToList();

        if (meaningfulEvidence.Count == 0)
        {
            return Task.FromResult(
                new AgentResult<DraftResponse>(
                    false,
                    null,
                    "A response cannot be drafted because supporting evidence is insufficient."));
        }

        var citations = meaningfulEvidence
            .Select(x => new Citation(
                x.DocumentId,
                x.ChunkId,
                x.DocumentTitle,
                x.PageNumber))
            .ToArray();

        var response = $"""
            Thank you for your inquiry.

            Based on the available government guidance, your request should first be
            identified as a specific citizen service and your eligibility should be
            verified against the applicable requirements.

            The general procedure supported by the available guidance is:

            {string.Join(
                Environment.NewLine,
                procedure.Steps.Select(
                    (step, index) => $"{index + 1}. {step}"))}

            The available evidence does not establish a final entitlement or
            obligation for your specific circumstances.

            No unsupported entitlement, obligation, fee, deadline, or eligibility
            decision has been inferred from the available evidence.

            A government officer should review the case if any eligibility
            requirement or procedure remains unclear.

            This response is a draft and requires officer approval before being
            provided as an official response.
            """;

        var draft = new DraftResponse(
            response,
            citations,
            RequiresOfficerApproval: true);

        return Task.FromResult(
            new AgentResult<DraftResponse>(
                true,
                draft,
                null));
    }
}
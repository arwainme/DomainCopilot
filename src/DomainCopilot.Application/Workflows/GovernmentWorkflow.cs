using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Workflows;

public sealed class GovernmentWorkflow : IGovernmentWorkflow
{
    private readonly IRetrievalService _retrievalService;
    private readonly IEligibilityIdentifier _eligibilityIdentifier;
    private readonly IProcedureResolver _procedureResolver;
    private readonly IResponseDrafter _responseDrafter;
    private readonly IApprovalService _approvalService;
    private readonly IAuditStore _auditStore;

    public GovernmentWorkflow(
        IRetrievalService retrievalService,
        IEligibilityIdentifier eligibilityIdentifier,
        IProcedureResolver procedureResolver,
        IResponseDrafter responseDrafter,
        IApprovalService approvalService,
        IAuditStore auditStore)
    {
        _retrievalService = retrievalService;
        _eligibilityIdentifier = eligibilityIdentifier;
        _procedureResolver = procedureResolver;
        _responseDrafter = responseDrafter;
        _approvalService = approvalService;
        _auditStore = auditStore;
    }

    public async Task<GovernmentWorkflowResult> ExecuteAsync(
        CitizenQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var runId = Guid.NewGuid();

        await _auditStore.StartRunAsync(
            runId,
            query,
            cancellationToken);

        try
        {
            // 1. Retrieve relevant evidence
            var evidence = await _retrievalService.SearchAsync(
                query.Situation,
                topK: 5,
                cancellationToken);

            await _auditStore.RecordStepAsync(
                runId,
                "Retrieval",
                "Search",
                query.Situation,
                $"Retrieved {evidence.Count} evidence chunks.",
                cancellationToken);

            // 2. Eligibility agent
            var eligibilityResult =
                await _eligibilityIdentifier.ExecuteAsync(
                    query,
                    evidence,
                    cancellationToken);

            if (!eligibilityResult.Success || eligibilityResult.Data is null)
            {
                throw new InvalidOperationException(
                    eligibilityResult.Error ??
                    "Eligibility agent failed.");
            }

            var eligibility = eligibilityResult.Data;

            await _auditStore.RecordStepAsync(
                runId,
                "EligibilityIdentifier",
                "IdentifyEligibility",
                query.Situation,
                eligibility.Explanation,
                cancellationToken);

            // 3. Procedure agent
            var procedureResult =
                await _procedureResolver.ExecuteAsync(
                    query,
                    eligibility,
                    evidence,
                    cancellationToken);

            if (!procedureResult.Success || procedureResult.Data is null)
            {
                throw new InvalidOperationException(
                    procedureResult.Error ??
                    "Procedure agent failed.");
            }

            var procedure = procedureResult.Data;

            await _auditStore.RecordStepAsync(
                runId,
                "ProcedureResolver",
                "ResolveProcedure",
                eligibility.Explanation,
                string.Join(Environment.NewLine, procedure.Steps),
                cancellationToken);

            // 4. Response drafting agent
            var draftResult =
                await _responseDrafter.ExecuteAsync(
                    query,
                    eligibility,
                    procedure,
                    evidence,
                    cancellationToken);

            if (!draftResult.Success || draftResult.Data is null)
            {
                throw new InvalidOperationException(
                    draftResult.Error ??
                    "Response drafter failed.");
            }

            var draft = draftResult.Data;

            await _auditStore.RecordStepAsync(
                runId,
                "ResponseDrafter",
                "DraftResponse",
                string.Join(Environment.NewLine, procedure.Steps),
                draft.ResponseText,
                cancellationToken);

            // 5. Human approval
            var approvalResult =
                await _approvalService.RequestApprovalAsync(
                    runId,
                    draft,
                    cancellationToken);

            await _auditStore.RecordStepAsync(
                runId,
                "Approval",
                "RequestApproval",
                draft.ResponseText,
                approvalResult.Status,
                cancellationToken);

            return new GovernmentWorkflowResult(
                runId,
                eligibility,
                procedure,
                draft,
                draft.RequiresOfficerApproval);
        }
        catch
        {
            throw;
        }
    }
}
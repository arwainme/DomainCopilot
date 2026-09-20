using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;
using DomainCopilot.Application.Tools;

namespace DomainCopilot.Application.Workflows;

public sealed class GovernmentWorkflow : IGovernmentWorkflow
{
    private readonly IRetrievalService _retrievalService;
    private readonly IEligibilityIdentifier _eligibilityIdentifier;
    private readonly IProcedureResolver _procedureResolver;
    private readonly IResponseDrafter _responseDrafter;
    private readonly IApprovalService _approvalService;
    private readonly IAuditStore _auditStore;
    private readonly ToolRegistry _toolRegistry;

    public GovernmentWorkflow(
        IRetrievalService retrievalService,
        IEligibilityIdentifier eligibilityIdentifier,
        IProcedureResolver procedureResolver,
        IResponseDrafter responseDrafter,
        IApprovalService approvalService,
        IAuditStore auditStore,
        ToolRegistry toolRegistry)
    {
        _retrievalService = retrievalService;
        _eligibilityIdentifier = eligibilityIdentifier;
        _procedureResolver = procedureResolver;
        _responseDrafter = responseDrafter;
        _approvalService = approvalService;
        _auditStore = auditStore;
        _toolRegistry = toolRegistry;
    }

    public async Task<GovernmentWorkflowResult> ExecuteAsync(
        CitizenQuery query,
        CancellationToken cancellationToken = default,
        Guid? runId = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        var currentRunId = runId ?? Guid.NewGuid();

        await _auditStore.StartRunAsync(
            currentRunId,
            query,
            cancellationToken);

        try
        {
            // 1. Retrieve relevant evidence through the orchestrator tool registry.
            var searchToolResult = await ExecuteToolAsync(
                currentRunId,
                "search_evidence",
                query.Situation,
                cancellationToken);

            var evidence =
                searchToolResult.Data as IReadOnlyList<EvidenceChunk>
                ?? Array.Empty<EvidenceChunk>();

            await _auditStore.RecordStepAsync(
                currentRunId,
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

            if (!eligibilityResult.Success ||
                eligibilityResult.Data is null)
            {
                var reason =
                    eligibilityResult.Error ??
                    "Insufficient evidence to determine eligibility.";

                await _auditStore.RecordStepAsync(
                    currentRunId,
                    "EligibilityIdentifier",
                    "Escalate",
                    query.Situation,
                    reason,
                    cancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    cancellationToken);

                return new GovernmentWorkflowResult(
                    currentRunId,
                    new EligibilityResult(
                        IsSupported: false,
                        Explanation: reason,
                        Evidence: evidence),
                    new ProcedureResult(
                        IsSupported: false,
                        Steps: Array.Empty<string>(),
                        Evidence: evidence),
                    null,
                    RequiresOfficerApproval: true);
            }

            var eligibility = eligibilityResult.Data;

            await _auditStore.RecordStepAsync(
                currentRunId,
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

            if (!procedureResult.Success ||
                procedureResult.Data is null)
            {
                var reason =
                    procedureResult.Error ??
                    "The procedure cannot be resolved from the available evidence.";

                await _auditStore.RecordStepAsync(
                    currentRunId,
                    "ProcedureResolver",
                    "Escalate",
                    eligibility.Explanation,
                    reason,
                    cancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    cancellationToken);

                return new GovernmentWorkflowResult(
                    currentRunId,
                    eligibility,
                    new ProcedureResult(
                        IsSupported: false,
                        Steps: Array.Empty<string>(),
                        Evidence: evidence),
                    null,
                    RequiresOfficerApproval: true);
            }

            var procedure = procedureResult.Data;

            await _auditStore.RecordStepAsync(
                currentRunId,
                "ProcedureResolver",
                "ResolveProcedure",
                eligibility.Explanation,
                string.Join(
                    Environment.NewLine,
                    procedure.Steps),
                cancellationToken);

            // 4. Response drafting agent
            var draftResult =
                await _responseDrafter.ExecuteAsync(
                    query,
                    eligibility,
                    procedure,
                    evidence,
                    cancellationToken);

            if (!draftResult.Success ||
                draftResult.Data is null)
            {
                var reason =
                    draftResult.Error ??
                    "A response cannot be drafted from the available evidence.";

                await _auditStore.RecordStepAsync(
                    currentRunId,
                    "ResponseDrafter",
                    "Escalate",
                    string.Join(
                        Environment.NewLine,
                        procedure.Steps),
                    reason,
                    cancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    cancellationToken);

                return new GovernmentWorkflowResult(
                    currentRunId,
                    eligibility,
                    procedure,
                    null,
                    RequiresOfficerApproval: true);
            }

            var draft = draftResult.Data;

            await _auditStore.RecordStepAsync(
                currentRunId,
                "ResponseDrafter",
                "DraftResponse",
                string.Join(
                    Environment.NewLine,
                    procedure.Steps),
                draft.ResponseText,
                cancellationToken);

            // 5. Human approval
            var approvalResult =
                await _approvalService.RequestApprovalAsync(
                    currentRunId,
                    draft,
                    cancellationToken);

            await _auditStore.RecordStepAsync(
                currentRunId,
                "Approval",
                "RequestApproval",
                draft.ResponseText,
                approvalResult.Status,
                cancellationToken);

            if (approvalResult.Status == "Pending")
            {
                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "WaitingForApproval",
                    cancellationToken);
            }
            else
            {
                await _auditStore.CompleteRunAsync(
                    currentRunId,
                    cancellationToken);
            }

            return new GovernmentWorkflowResult(
                currentRunId,
                eligibility,
                procedure,
                draft,
                draft.RequiresOfficerApproval);
        }
        catch (Exception ex)
        {
            await _auditStore.FailRunAsync(
                currentRunId,
                ex.Message,
                cancellationToken);

            throw;
        }
    }

    private async Task<ToolResult> ExecuteToolAsync(
        Guid runId,
        string toolName,
        string input,
        CancellationToken cancellationToken)
    {
        if (!_toolRegistry.TryGet(
                toolName,
                out var tool) ||
            tool is null)
        {
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not allowed.");
        }

        var result = await tool.ExecuteAsync(
            input,
            cancellationToken);

        await _auditStore.RecordStepAsync(
            runId,
            "Orchestrator",
            $"Tool:{toolName}",
            input,
            result.Output,
            cancellationToken);

        return result;
    }
}
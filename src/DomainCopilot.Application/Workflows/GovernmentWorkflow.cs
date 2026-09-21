using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Agents;
using DomainCopilot.Application.Configuration;
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
    private readonly WorkflowOptions _options;

    public GovernmentWorkflow(
        IRetrievalService retrievalService,
        IEligibilityIdentifier eligibilityIdentifier,
        IProcedureResolver procedureResolver,
        IResponseDrafter responseDrafter,
        IApprovalService approvalService,
        IAuditStore auditStore,
        ToolRegistry toolRegistry,
        WorkflowOptions options)
    {
        _retrievalService = retrievalService;
        _eligibilityIdentifier = eligibilityIdentifier;
        _procedureResolver = procedureResolver;
        _responseDrafter = responseDrafter;
        _approvalService = approvalService;
        _auditStore = auditStore;
        _toolRegistry = toolRegistry;
        _options = options;
    }

    public async Task<GovernmentWorkflowResult> ExecuteAsync(
        CitizenQuery query,
        CancellationToken cancellationToken = default,
        Guid? runId = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        var currentRunId = runId ?? Guid.NewGuid();

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(
            TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var workflowCancellationToken =
            timeoutCts.Token;

        var iteration = 0;

        await _auditStore.StartRunAsync(
            currentRunId,
            query,
            workflowCancellationToken);

        try
        {
            iteration = await RecordIterationAsync(
                currentRunId,
                iteration,
                "Retrieval",
                workflowCancellationToken);

            var searchToolResult = await ExecuteWithRetryAsync(
                () => ExecuteToolAsync(
                    currentRunId,
                    "search_evidence",
                    query.Situation,
                    workflowCancellationToken),
                workflowCancellationToken);

            var evidence =
                searchToolResult.Data as IReadOnlyList<EvidenceChunk>
                ?? Array.Empty<EvidenceChunk>();

            await _auditStore.RecordStepAsync(
                currentRunId,
                "Retrieval",
                "Search",
                query.Situation,
                $"Retrieved {evidence.Count} evidence chunks.",
                workflowCancellationToken);

            iteration = await RecordIterationAsync(
                currentRunId,
                iteration,
                "EligibilityIdentifier",
                workflowCancellationToken);

            var eligibilityResult = await ExecuteWithRetryAsync(
                () => _eligibilityIdentifier.ExecuteAsync(
                    query,
                    evidence,
                    workflowCancellationToken),
                workflowCancellationToken);

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
                    workflowCancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    workflowCancellationToken);

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
                workflowCancellationToken);

            iteration = await RecordIterationAsync(
                currentRunId,
                iteration,
                "ProcedureResolver",
                workflowCancellationToken);

            var procedureResult = await ExecuteWithRetryAsync(
                () => _procedureResolver.ExecuteAsync(
                    query,
                    eligibility,
                    evidence,
                    workflowCancellationToken),
                workflowCancellationToken);

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
                    workflowCancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    workflowCancellationToken);

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
                workflowCancellationToken);

            iteration = await RecordIterationAsync(
                currentRunId,
                iteration,
                "ResponseDrafter",
                workflowCancellationToken);

            var draftResult = await ExecuteWithRetryAsync(
                () => _responseDrafter.ExecuteAsync(
                    query,
                    eligibility,
                    procedure,
                    evidence,
                    workflowCancellationToken),
                workflowCancellationToken);

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
                    workflowCancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    workflowCancellationToken);

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
                workflowCancellationToken);

            iteration = await RecordIterationAsync(
                currentRunId,
                iteration,
                "Approval",
                workflowCancellationToken);

            var approvalInput = JsonSerializer.Serialize(new
            {
                RunId = currentRunId,
                Draft = draft
            });

            var approvalToolResult = await ExecuteWithRetryAsync(
                () => ExecuteToolAsync(
                    currentRunId,
                    "submit_for_approval",
                    approvalInput,
                    workflowCancellationToken),
                workflowCancellationToken);

            if (!approvalToolResult.Success)
            {
                await _auditStore.RecordStepAsync(
                    currentRunId,
                    "Approval",
                    "ApprovalGuardBlocked",
                    draft.ResponseText,
                    approvalToolResult.Output,
                    workflowCancellationToken);

                await _auditStore.SetStatusAsync(
                    currentRunId,
                    "Escalated",
                    workflowCancellationToken);

                return new GovernmentWorkflowResult(
                    currentRunId,
                    eligibility,
                    procedure,
                    draft,
                    RequiresOfficerApproval: true);
            }

            await _auditStore.RecordStepAsync(
                currentRunId,
                "Approval",
                "SubmitForApproval",
                draft.ResponseText,
                approvalToolResult.Output,
                workflowCancellationToken);

            await _auditStore.SetStatusAsync(
                currentRunId,
                "WaitingForApproval",
                workflowCancellationToken);

            return new GovernmentWorkflowResult(
                currentRunId,
                eligibility,
                procedure,
                draft,
                draft.RequiresOfficerApproval);
        }
        catch (OperationCanceledException) when (
            workflowCancellationToken.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            await _auditStore.RecordStepAsync(
                currentRunId,
                "Orchestrator",
                "Timeout",
                query.Situation,
                $"Workflow exceeded the configured timeout of {_options.TimeoutSeconds} seconds.",
                CancellationToken.None);

            await _auditStore.SetStatusAsync(
                currentRunId,
                "TimedOut",
                CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            await _auditStore.FailRunAsync(
                currentRunId,
                ex.Message,
                CancellationToken.None);

            throw;
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 0;
             attempt <= _options.MaxRetries;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt >= _options.MaxRetries)
                {
                    break;
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(250 * (attempt + 1)),
                    cancellationToken);
            }
        }

        throw lastException ??
              new InvalidOperationException(
                  "Workflow operation failed after all retry attempts.");
    }

    private async Task<int> RecordIterationAsync(
        Guid runId,
        int currentIteration,
        string stage,
        CancellationToken cancellationToken)
    {
        var nextIteration = currentIteration + 1;

        if (nextIteration > _options.MaxIterations)
        {
            await _auditStore.RecordStepAsync(
                runId,
                "Orchestrator",
                "MaxIterationsExceeded",
                stage,
                $"Maximum workflow iterations ({_options.MaxIterations}) exceeded.",
                CancellationToken.None);

            await _auditStore.SetStatusAsync(
                runId,
                "Escalated",
                CancellationToken.None);

            throw new InvalidOperationException(
                $"Maximum workflow iterations ({_options.MaxIterations}) exceeded.");
        }

        await _auditStore.RecordStepAsync(
            runId,
            "Orchestrator",
            $"Iteration:{nextIteration}",
            stage,
            $"Workflow iteration {nextIteration} of {_options.MaxIterations}.",
            cancellationToken);

        return nextIteration;
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
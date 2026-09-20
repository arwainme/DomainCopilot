using DomainCopilot.Application.Abstractions;

namespace DomainCopilot.Application.Workflows;

public sealed class ReplayService : IReplayService
{
    private readonly IAuditStore _auditStore;
    private readonly IGovernmentWorkflow _workflow;

    public ReplayService(
        IAuditStore auditStore,
        IGovernmentWorkflow workflow)
    {
        _auditStore = auditStore;
        _workflow = workflow;
    }

    public async Task<AuditRunDto?> ReplayAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var originalRun = await _auditStore.GetRunAsync(
            runId,
            cancellationToken);

        if (originalRun is null)
        {
            return null;
        }

        // Create a new run so the original audit record is preserved.
        var replayRunId = Guid.NewGuid();

        await _workflow.ExecuteAsync(
            originalRun.Query,
            cancellationToken,
            replayRunId);

        return await _auditStore.GetRunAsync(
            replayRunId,
            cancellationToken);
    }
}
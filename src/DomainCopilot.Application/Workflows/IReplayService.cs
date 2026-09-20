using DomainCopilot.Application.Abstractions;

namespace DomainCopilot.Application.Workflows;

public interface IReplayService
{
    Task<AuditRunDto?> ReplayAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}
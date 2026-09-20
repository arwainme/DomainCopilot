using System.Collections.Concurrent;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;

public sealed class ApprovalService : IApprovalService
{
    private readonly ConcurrentDictionary<Guid, ApprovalState> _requests = new();

    public Task<ApprovalResult> RequestApprovalAsync(
        Guid runId,
        DraftResponse draft,
        CancellationToken cancellationToken = default)
    {
        var state = new ApprovalState(
            runId,
            "Pending",
            null,
            null,
            null);

        _requests[runId] = state;

        return Task.FromResult(
            new ApprovalResult(runId, "Pending"));
    }

    public Task ApproveAsync(
        Guid runId,
        string officerId,
        CancellationToken cancellationToken = default)
    {
        if (!_requests.TryGetValue(runId, out var request))
        {
            throw new InvalidOperationException(
                $"Approval request for run '{runId}' was not found.");
        }

        _requests[runId] = request with
        {
            Status = "Approved",
            OfficerId = officerId,
            DecisionAt = DateTime.UtcNow
        };

        return Task.CompletedTask;
    }

    public Task RejectAsync(
        Guid runId,
        string officerId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_requests.TryGetValue(runId, out var request))
        {
            throw new InvalidOperationException(
                $"Approval request for run '{runId}' was not found.");
        }

        _requests[runId] = request with
        {
            Status = "Rejected",
            OfficerId = officerId,
            Reason = reason,
            DecisionAt = DateTime.UtcNow
        };

        return Task.CompletedTask;
    }

    private sealed record ApprovalState(
        Guid RunId,
        string Status,
        string? OfficerId,
        string? Reason,
        DateTime? DecisionAt);
}
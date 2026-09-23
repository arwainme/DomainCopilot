using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Abstractions;

public interface IAuditStore
{

    Task StartRunAsync(
        Guid runId,
        CitizenQuery query,
        CancellationToken cancellationToken = default);

    Task RecordStepAsync(
        Guid runId,
        string agentName,
        string action,
        string input,
        string output,
        CancellationToken cancellationToken = default);

    Task CompleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task FailRunAsync(
        Guid runId,
        string reason,
        CancellationToken cancellationToken = default);
    Task SetStatusAsync(
    Guid runId,
    string status,
    CancellationToken cancellationToken = default);

    Task<AuditRunDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

public sealed record AuditRunDto(
    Guid RunId,
    CitizenQuery Query,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? FailureReason,
    IReadOnlyCollection<AuditStepDto> Steps);

public sealed record AuditStepDto(
    int Sequence,
    string AgentName,
    string Action,
    string Input,
    string Output,
    DateTime CreatedAt);

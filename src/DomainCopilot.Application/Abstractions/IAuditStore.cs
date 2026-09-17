using System;
using System.Collections.Generic;
using System.Text;
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

    Task<AuditRunDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default);
}

public sealed record AuditRunDto(
    Guid RunId,
    CitizenQuery Query,
    IReadOnlyCollection<AuditStepDto> Steps);

public sealed record AuditStepDto(
    string AgentName,
    string Action,
    string Input,
    string Output);

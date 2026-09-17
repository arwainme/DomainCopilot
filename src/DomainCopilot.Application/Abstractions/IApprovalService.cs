using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Abstractions;

public interface IApprovalService
{
    Task<ApprovalResult> RequestApprovalAsync(
        Guid runId,
        DraftResponse draft,
        CancellationToken cancellationToken = default);

    Task ApproveAsync(
        Guid runId,
        string officerId,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        Guid runId,
        string officerId,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovalResult(
    Guid RunId,
    string Status);
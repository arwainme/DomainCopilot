using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entities;

public class ApprovalRequest
{
    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public string ProposedResponse { get; private set; }

    public ApprovalStatus Status { get; private set; }

    public string? ReviewerComment { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ResolvedAt { get; private set; }

    private ApprovalRequest()
    {
        ProposedResponse = string.Empty;
    }

    public ApprovalRequest(Guid runId, string proposedResponse)
    {
        Id = Guid.NewGuid();
        RunId = runId;
        ProposedResponse = proposedResponse;
        Status = ApprovalStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve(string? comment = null)
    {
        Status = ApprovalStatus.Approved;
        ReviewerComment = comment;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Reject(string comment)
    {
        Status = ApprovalStatus.Rejected;
        ReviewerComment = comment;
        ResolvedAt = DateTime.UtcNow;
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Domain.Entities;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Tests.Entities;

public class ApprovalRequestTests
{
    [Fact]
    public void CreateApprovalRequest_ShouldBePending()
    {
        var runId = Guid.NewGuid();

        var approval = new ApprovalRequest(
            runId,
            "Proposed official response.");

        Assert.NotEqual(Guid.Empty, approval.Id);
        Assert.Equal(runId, approval.RunId);
        Assert.Equal(
            "Proposed official response.",
            approval.ProposedResponse);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
        Assert.Null(approval.ReviewerComment);
        Assert.Null(approval.ResolvedAt);
    }

    [Fact]
    public void Approve_ShouldSetApprovedStatus()
    {
        var approval = new ApprovalRequest(
            Guid.NewGuid(),
            "Proposed official response.");

        approval.Approve("Looks correct.");

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal("Looks correct.", approval.ReviewerComment);
        Assert.NotNull(approval.ResolvedAt);
    }

    [Fact]
    public void Reject_ShouldSetRejectedStatusAndComment()
    {
        var approval = new ApprovalRequest(
            Guid.NewGuid(),
            "Proposed official response.");

        approval.Reject("Missing required evidence.");

        Assert.Equal(ApprovalStatus.Rejected, approval.Status);
        Assert.Equal(
            "Missing required evidence.",
            approval.ReviewerComment);
        Assert.NotNull(approval.ResolvedAt);
    }
}
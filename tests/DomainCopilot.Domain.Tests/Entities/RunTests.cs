using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Domain.Entities;
using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Tests.Entities;

public class RunTests
{
    [Fact]
    public void CreateRun_ShouldStartWithStartedStatus()
    {
        var run = new Run("correlation-123");

        Assert.NotEqual(Guid.Empty, run.Id);
        Assert.Equal("correlation-123", run.CorrelationId);
        Assert.Equal(RunStatus.Started, run.Status);
        Assert.NotEqual(default, run.StartedAt);
        Assert.Null(run.CompletedAt);
    }

    [Fact]
    public void RunWorkflow_ShouldMoveThroughApprovalToCompleted()
    {
        var run = new Run("correlation-123");

        run.Start();
        Assert.Equal(RunStatus.InProgress, run.Status);

        run.WaitForApproval();
        Assert.Equal(RunStatus.WaitingForApproval, run.Status);

        run.Approve();
        Assert.Equal(RunStatus.Approved, run.Status);

        run.Complete();

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public void Reject_ShouldSetRejectedStatusAndReason()
    {
        var run = new Run("correlation-123");

        run.Reject("Officer rejected the proposed response.");

        Assert.Equal(RunStatus.Rejected, run.Status);
        Assert.Equal(
            "Officer rejected the proposed response.",
            run.FailureReason);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public void Fail_ShouldSetFailedStatusAndReason()
    {
        var run = new Run("correlation-123");

        run.Fail("LLM provider unavailable.");

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("LLM provider unavailable.", run.FailureReason);
        Assert.NotNull(run.CompletedAt);
    }
}
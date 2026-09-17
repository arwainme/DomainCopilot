using System;
using System.Collections.Generic;
using System.Text;

using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entities;

public class Run
{
    public Guid Id { get; private set; }

    public string CorrelationId { get; private set; }

    public RunStatus Status { get; private set; }

    public DateTime StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    private Run()
    {
        CorrelationId = string.Empty;
    }

    public Run(string correlationId)
    {
        Id = Guid.NewGuid();
        CorrelationId = correlationId;
        Status = RunStatus.Started;
        StartedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        Status = RunStatus.InProgress;
    }

    public void WaitForApproval()
    {
        Status = RunStatus.WaitingForApproval;
    }

    public void Approve()
    {
        Status = RunStatus.Approved;
    }

    public void Reject(string reason)
    {
        Status = RunStatus.Rejected;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = RunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = RunStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }
}
namespace DomainCopilot.Domain.Enums;

public enum RunStatus
{
    Started,
    InProgress,
    WaitingForApproval,
    Approved,
    Rejected,
    Completed,
    Failed
}
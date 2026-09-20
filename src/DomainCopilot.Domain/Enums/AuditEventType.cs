using System;
using System.Collections.Generic;
using System.Text;
namespace DomainCopilot.Domain.Enums;

public enum AuditEventType
{
    RunStarted,
    RetrievalCompleted,
    AgentStarted,
    AgentCompleted,
    ToolCalled,
    ApprovalRequested,
    ApprovalApproved,
    ApprovalRejected,
    RunCompleted,
    RunFailed
}
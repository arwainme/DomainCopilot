using System;
using System.Collections.Generic;
using System.Text;

using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entities;

public class AuditEvent
{
    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public AuditEventType EventType { get; private set; }

    public string Description { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private AuditEvent()
    {
        Description = string.Empty;
    }

    public AuditEvent(
        Guid runId,
        AuditEventType eventType,
        string description)
    {
        Id = Guid.NewGuid();
        RunId = runId;
        EventType = eventType;
        Description = description;
        CreatedAt = DateTime.UtcNow;
    }
}

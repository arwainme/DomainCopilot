using DomainCopilot.Domain.Enums;

namespace DomainCopilot.Domain.Entities;

public class Document
{
    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Source { get; private set; } = string.Empty;

    public string? Version { get; private set; }

    public DocumentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private Document()
    {
    }

    public Document(
        string title,
        string source,
        string? version = null)
    {
        Id = Guid.NewGuid();
        Title = title;
        Source = source;
        Version = version;
        Status = DocumentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        Status = DocumentStatus.Processing;
        FailureReason = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted()
    {
        Status = DocumentStatus.Completed;
        FailureReason = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string failureReason)
    {
        Status = DocumentStatus.Failed;
        FailureReason = failureReason;
        UpdatedAt = DateTime.UtcNow;
    }
}
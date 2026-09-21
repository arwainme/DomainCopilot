namespace DomainCopilot.Application.Configuration;

public sealed class WorkflowOptions
{
    public int MaxIterations { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxRetries { get; set; } = 2;
}
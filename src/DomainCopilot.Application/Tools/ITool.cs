namespace DomainCopilot.Application.Tools;

public interface ITool
{
    string Name { get; }

    Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default);
}

public sealed record ToolResult(
    bool Success,
    string Output,
    bool HasSideEffect = false,
    object? Data = null);
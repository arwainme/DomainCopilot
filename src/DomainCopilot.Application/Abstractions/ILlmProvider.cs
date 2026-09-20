namespace DomainCopilot.Application.Abstractions;

public interface ILlmProvider
{
    Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    Task<string> CallWithToolsAsync(
        string prompt,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}

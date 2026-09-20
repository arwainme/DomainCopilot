using DomainCopilot.Application.Abstractions;

namespace DomainCopilot.Application.Tools;

public sealed class SearchEvidenceTool : ITool
{
    private readonly IRetrievalService _retrievalService;

    public SearchEvidenceTool(
        IRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public string Name => "search_evidence";

    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var evidence = await _retrievalService.SearchAsync(
            input,
            5,
            cancellationToken);

        return new ToolResult(
            Success: true,
            Output: $"Retrieved {evidence.Count} evidence chunks.",
            Data: evidence);
    }
}
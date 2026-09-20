using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Tools;

public sealed class CheckEligibilityTool : ITool
{
    private readonly IEligibilityIdentifier _agent;

    public CheckEligibilityTool(
        IEligibilityIdentifier agent)
    {
        _agent = agent;
    }

    public string Name => "check_eligibility";

    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var query = new CitizenQuery(input);

        var result = await _agent.ExecuteAsync(
            query,
            Array.Empty<EvidenceChunk>(),
            cancellationToken);

        return new ToolResult(
            result.Success,
            result.Success
                ? result.Data?.Explanation ?? "Eligibility evaluated."
                : result.Error ?? "Eligibility evaluation failed.");
    }
}

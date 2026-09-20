using DomainCopilot.Application.Agents;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Tools;

public sealed class ResolveProcedureTool : ITool
{
    private readonly IProcedureResolver _agent;

    public ResolveProcedureTool(
        IProcedureResolver agent)
    {
        _agent = agent;
    }

    public string Name => "resolve_procedure";

    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var query = new CitizenQuery(input);

        var eligibility = new EligibilityResult(
            IsSupported: true,
            Explanation: "Procedure resolution requested by the orchestrator.",
            Evidence: Array.Empty<EvidenceChunk>());

        var result = await _agent.ExecuteAsync(
            query,
            eligibility,
            Array.Empty<EvidenceChunk>(),
            cancellationToken);

        return new ToolResult(
            result.Success,
            result.Success
                ? string.Join(
                    Environment.NewLine,
                    result.Data?.Steps ?? Array.Empty<string>())
                : result.Error ?? "Procedure resolution failed.");
    }
}
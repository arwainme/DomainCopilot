using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Tools;

public sealed class SubmitApprovalTool : ITool
{
    private readonly IApprovalService _approvalService;

    public SubmitApprovalTool(
        IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    public string Name => "submit_for_approval";

    public async Task<ToolResult> ExecuteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.Parse(input);

        var draft = new DraftResponse(
            "Draft submitted for officer approval.",
            Array.Empty<Citation>(),
            true);

        var result = await _approvalService.RequestApprovalAsync(
            runId,
            draft,
            cancellationToken);

        return new ToolResult(
            true,
            $"Approval request created with status: {result.Status}",
            HasSideEffect: true);
    }
}

using System.Text.Json;
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
        ApprovalToolRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<ApprovalToolRequest>(
                input,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return new ToolResult(
                false,
                "Invalid approval request.",
                HasSideEffect: false);
        }

        if (request is null)
        {
            return new ToolResult(
                false,
                "Invalid approval request.",
                HasSideEffect: false);
        }

        if (request.RunId == Guid.Empty)
        {
            return new ToolResult(
                false,
                "A valid run ID is required.",
                HasSideEffect: false);
        }

        if (request.Draft is null)
        {
            return new ToolResult(
                false,
                "A draft response is required before approval submission.",
                HasSideEffect: false);
        }

        // Approval guard:
        // Only drafts that explicitly require officer approval
        // may trigger this side effect.
        if (!request.Draft.RequiresOfficerApproval)
        {
            return new ToolResult(
                false,
                "Approval submission is blocked because officer approval is not required.",
                HasSideEffect: false);
        }

        var result = await _approvalService.RequestApprovalAsync(
            request.RunId,
            request.Draft,
            cancellationToken);

        return new ToolResult(
            true,
            $"Approval request created with status: {result.Status}",
            HasSideEffect: true);
    }

    private sealed record ApprovalToolRequest(
        Guid RunId,
        DraftResponse Draft);
}
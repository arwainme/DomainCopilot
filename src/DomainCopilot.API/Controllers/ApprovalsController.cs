using DomainCopilot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/runs/{runId}/")]
[Authorize(Roles = "Officer")]
public class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;
    private readonly IAuditStore _auditStore;

    public ApprovalsController(
        IApprovalService approvalService,
        IAuditStore auditStore)
    {
        _approvalService = approvalService;
        _auditStore = auditStore;
    }

    [HttpPost("approve")]
    public async Task<IActionResult> Approve(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var officerId = "demo-officer";

        await _approvalService.ApproveAsync(
            runId,
            officerId,
            cancellationToken);

        await _auditStore.RecordStepAsync(
            runId,
            "Officer",
            "ApproveResponse",
            officerId,
            "Approved",
            cancellationToken);

        await _auditStore.SetStatusAsync(
            runId,
            "Approved",
            cancellationToken);

        return Ok(new
        {
            runId,
            status = "Approved",
            officerId
        });
    }

    [HttpPost("reject")]
    public async Task<IActionResult> Reject(
        Guid runId,
        [FromBody] RejectRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new
            {
                message = "A rejection reason is required."
            });
        }

        var officerId = "demo-officer";

        await _approvalService.RejectAsync(
            runId,
            officerId,
            request.Reason,
            cancellationToken);

        await _auditStore.RecordStepAsync(
            runId,
            "Officer",
            "RejectResponse",
            officerId,
            request.Reason,
            cancellationToken);

        await _auditStore.SetStatusAsync(
            runId,
            "Rejected",
            cancellationToken);

        return Ok(new
        {
            runId,
            status = "Rejected",
            officerId,
            reason = request.Reason
        });
    }

    [HttpPost("edit-and-approve")]
    public async Task<IActionResult> EditAndApprove(
        Guid runId,
        [FromBody] EditAndApproveRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EditedResponse))
        {
            return BadRequest(new
            {
                message = "Edited response is required."
            });
        }

        var officerId = "demo-officer";

        await _approvalService.EditAndApproveAsync(
            runId,
            officerId,
            request.EditedResponse,
            cancellationToken);

        await _auditStore.RecordStepAsync(
            runId,
            "Officer",
            "EditAndApproveResponse",
            officerId,
            request.EditedResponse,
            cancellationToken);

        await _auditStore.SetStatusAsync(
            runId,
            "Approved",
            cancellationToken);

        return Ok(new
        {
            runId,
            status = "Approved",
            officerId,
            edited = true,
            response = request.EditedResponse
        });
    }

    public sealed record RejectRequest(string Reason);

    public sealed record EditAndApproveRequest(
        string EditedResponse);
}
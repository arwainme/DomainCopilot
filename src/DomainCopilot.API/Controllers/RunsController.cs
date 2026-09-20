using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/runs")]
public sealed class RunsController : ControllerBase
{
    private readonly IAuditStore _auditStore;
    private readonly IReplayService _replayService;

    public RunsController(
        IAuditStore auditStore,
        IReplayService replayService)
    {
        _auditStore = auditStore;
        _replayService = replayService;
    }

    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetRun(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _auditStore.GetRunAsync(
            runId,
            cancellationToken);

        if (run is null)
        {
            return NotFound(new
            {
                message = $"Run '{runId}' was not found."
            });
        }

        return Ok(run);
    }

    [HttpPost("{runId:guid}/replay")]
    public async Task<IActionResult> Replay(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _replayService.ReplayAsync(
            runId,
            cancellationToken);

        if (run is null)
        {
            return NotFound(new
            {
                message = $"Run '{runId}' was not found."
            });
        }

        return Ok(new
        {
            message = "Run replayed successfully.",
            run
        });
    }
}
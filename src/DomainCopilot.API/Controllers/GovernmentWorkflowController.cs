using DomainCopilot.Application.DTOs;
using DomainCopilot.Application.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DomainCopilot.API.Controllers;

[Authorize]
[ApiController]
[Route("api/workflows/government")]
public sealed class GovernmentWorkflowController : ControllerBase
{
    private readonly IGovernmentWorkflow _workflow;

    public GovernmentWorkflowController(
        IGovernmentWorkflow workflow)
    {
        _workflow = workflow;
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] CitizenQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _workflow.ExecuteAsync(
            query,
            cancellationToken);

        return Ok(result);
    }
}


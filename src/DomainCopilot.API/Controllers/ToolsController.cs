using DomainCopilot.Application.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/tools")]
[Authorize]
public sealed class ToolsController : ControllerBase
{
    private readonly ToolRegistry _toolRegistry;

    public ToolsController(ToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    [HttpGet]
    public IActionResult GetTools()
    {
        return Ok(new
        {
            tools = _toolRegistry.Names
        });
    }
}
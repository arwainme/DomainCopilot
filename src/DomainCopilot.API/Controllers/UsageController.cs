using DomainCopilot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/usage")]
[Authorize(Roles = "Officer")]
public sealed class UsageController : ControllerBase
{
    private readonly IUsageTracker _usageTracker;

    public UsageController(IUsageTracker usageTracker)
    {
        _usageTracker = usageTracker;
    }

    [HttpGet]
    public IActionResult GetUsage()
    {
        var usage = _usageTracker.GetAll();

        return Ok(new
        {
            totalRequests = usage.Count,
            totalInputTokens = usage.Sum(x => x.InputTokens),
            totalOutputTokens = usage.Sum(x => x.OutputTokens),
            estimatedCostUsd = usage.Sum(x => x.EstimatedCostUsd),
            records = usage
        });
    }
}
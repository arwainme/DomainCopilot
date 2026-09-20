using DomainCopilot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/llm")]
[Authorize]
public sealed class StreamingController : ControllerBase
{
    private readonly ILlmProvider _llmProvider;

    public StreamingController(ILlmProvider llmProvider)
    {
        _llmProvider = llmProvider;
    }

    [HttpGet("stream")]
    public async Task Stream(
        [FromQuery] string prompt,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/plain; charset=utf-8";

        await foreach (var chunk in _llmProvider.StreamAsync(
            prompt,
            cancellationToken))
        {
            await Response.WriteAsync(
                chunk,
                cancellationToken);

            await Response.Body.FlushAsync(
                cancellationToken);
        }
    }
}
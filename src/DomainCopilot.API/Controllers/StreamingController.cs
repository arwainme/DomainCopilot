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
    private readonly ILogger<StreamingController> _logger;

    public StreamingController(
        ILlmProvider llmProvider,
        ILogger<StreamingController> logger)
    {
        _llmProvider = llmProvider;
        _logger = logger;
    }

    [HttpGet("stream")]
    public async Task Stream(
        [FromQuery] string prompt,
        CancellationToken cancellationToken)
    {
        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                HttpContext.RequestAborted);

        var streamCancellationToken = linkedCts.Token;

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await Response.StartAsync(streamCancellationToken);

            await foreach (var chunk in _llmProvider.StreamAsync(
                prompt,
                streamCancellationToken))
            {
                await Response.WriteAsync(
                    $"data: {chunk}\n\n",
                    streamCancellationToken);

                await Response.Body.FlushAsync(
                    streamCancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "LLM streaming request was cancelled by the client. CorrelationId: {CorrelationId}",
                HttpContext.TraceIdentifier);
        }
    }
}
using DomainCopilot.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.API.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly DocumentIngestionService _ingestionService;

    public DocumentsController(
        DocumentIngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    [HttpPost("ingest")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Ingest(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                message = "A non-empty file is required."
            });
        }

        var extension =
            Path.GetExtension(file.FileName)
                .ToLowerInvariant();

        if (extension is not ".pdf" and not ".txt")
        {
            return BadRequest(new
            {
                message = "Only PDF and TXT files are supported."
            });
        }

        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "DomainCopilot");

        Directory.CreateDirectory(tempDirectory);

        var tempPath =
            Path.Combine(
                tempDirectory,
                $"{Guid.NewGuid():N}{extension}");

        try
        {
            await using (var stream =
                System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(
                    stream,
                    cancellationToken);
            }

            var chunks =
                await _ingestionService.IngestAsync(
                    tempPath,
                    cancellationToken);

            return Ok(new
            {
                fileName = file.FileName,
                chunkCount = chunks.Count,
                documentId =
                    chunks.Count > 0
                        ? chunks[0].DocumentId
                        : (Guid?)null,
                message = "Document ingested successfully."
            });
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }
        }
    }
}
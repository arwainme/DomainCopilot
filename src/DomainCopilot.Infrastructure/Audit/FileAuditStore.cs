using System.Text.Json;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Infrastructure.Audit;

public sealed class FileAuditStore : IAuditStore
{
    private readonly string _auditDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileAuditStore()
    {
        _auditDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "data",
            "audit");

        Directory.CreateDirectory(_auditDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public async Task StartRunAsync(
        Guid runId,
        CitizenQuery query,
        CancellationToken cancellationToken = default)
    {
        var auditRun = new AuditRunDto(
            runId,
            query,
            "Started",
            DateTime.UtcNow,
            null,
            null,
            Array.Empty<AuditStepDto>());

        await SaveAsync(auditRun, cancellationToken);
    }

    public async Task RecordStepAsync(
        Guid runId,
        string agentName,
        string action,
        string input,
        string output,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var existing = await LoadAsync(runId, cancellationToken);

            if (existing is null)
            {
                throw new InvalidOperationException(
                    $"Audit run '{runId}' was not found.");
            }

            var nextSequence = existing.Steps.Count + 1;

            var step = new AuditStepDto(
                nextSequence,
                agentName,
                action,
                input,
                output,
                DateTime.UtcNow);

            var updatedSteps = existing.Steps
                .Append(step)
                .ToArray();

            var updatedRun = existing with
            {
                Status = "InProgress",
                Steps = updatedSteps
            };

            await SaveUnlockedAsync(updatedRun, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CompleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var existing = await LoadAsync(runId, cancellationToken);

            if (existing is null)
            {
                throw new InvalidOperationException(
                    $"Audit run '{runId}' was not found.");
            }

            var updatedRun = existing with
            {
                Status = "Completed",
                CompletedAt = DateTime.UtcNow
            };

            await SaveUnlockedAsync(updatedRun, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task FailRunAsync(
        Guid runId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var existing = await LoadAsync(runId, cancellationToken);

            if (existing is null)
            {
                throw new InvalidOperationException(
                    $"Audit run '{runId}' was not found.");
            }

            var updatedRun = existing with
            {
                Status = "Failed",
                FailureReason = reason,
                CompletedAt = DateTime.UtcNow
            };

            await SaveUnlockedAsync(updatedRun, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SetStatusAsync(
    Guid runId,
    string status,
    CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            var existing = await LoadAsync(runId, cancellationToken);

            if (existing is null)
            {
                throw new InvalidOperationException(
                    $"Audit run '{runId}' was not found.");
            }

            var updatedRun = existing with
            {
                Status = status
            };

            await SaveUnlockedAsync(updatedRun, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AuditRunDto?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        return await LoadAsync(runId, cancellationToken);
    }

    private async Task SaveAsync(
        AuditRunDto auditRun,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            await SaveUnlockedAsync(auditRun, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveUnlockedAsync(
        AuditRunDto auditRun,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(auditRun.RunId);

        var json = JsonSerializer.Serialize(
            auditRun,
            _jsonOptions);

        await File.WriteAllTextAsync(
            filePath,
            json,
            cancellationToken);
    }

    private async Task<AuditRunDto?> LoadAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(runId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(
            filePath,
            cancellationToken);

        return JsonSerializer.Deserialize<AuditRunDto>(
            json,
            _jsonOptions);
    }

    private string GetFilePath(Guid runId)
    {
        return Path.Combine(
            _auditDirectory,
            $"{runId}.json");
    }
}
using System.Collections.Concurrent;
using DomainCopilot.Application.Abstractions;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Infrastructure.Usage;

public sealed class UsageTracker : IUsageTracker
{
    private readonly ConcurrentBag<LlmUsage> _usage = new();

    public void Record(LlmUsage usage)
    {
        _usage.Add(usage);
    }

    public IReadOnlyCollection<LlmUsage> GetAll()
    {
        return _usage.ToArray();
    }
}
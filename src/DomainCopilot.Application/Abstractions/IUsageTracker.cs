using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Abstractions;

public interface IUsageTracker
{
    void Record(LlmUsage usage);

    IReadOnlyCollection<LlmUsage> GetAll();
}
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Abstractions;

public interface IRetrievalService
{
    Task<IReadOnlyList<EvidenceChunk>> SearchAsync(
        string query,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

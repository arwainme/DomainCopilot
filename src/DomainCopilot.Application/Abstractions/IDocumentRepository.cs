using System;
using System.Collections.Generic;
using System.Text;

using DomainCopilot.Domain.Entities;

namespace DomainCopilot.Application.Abstractions;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Document document,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
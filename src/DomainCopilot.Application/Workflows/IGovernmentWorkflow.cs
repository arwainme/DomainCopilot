using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Application.DTOs;
namespace DomainCopilot.Application.Workflows;
public interface IGovernmentWorkflow
{
  Task<GovernmentWorkflowResult> ExecuteAsync(
    CitizenQuery query,
    CancellationToken cancellationToken = default,
    Guid? runId = null);
}
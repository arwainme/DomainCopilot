using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Application.Agents;

public sealed record AgentResult<T>(
    bool Success,
    T? Data,
    string? Error);
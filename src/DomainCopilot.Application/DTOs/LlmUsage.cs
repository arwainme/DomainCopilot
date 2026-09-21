using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Application.DTOs;

public sealed record LlmUsage(
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCostUsd);


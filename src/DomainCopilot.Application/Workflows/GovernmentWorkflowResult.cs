using System;
using System.Collections.Generic;
using System.Text;
using DomainCopilot.Application.DTOs;

namespace DomainCopilot.Application.Workflows;

public sealed record GovernmentWorkflowResult(
    Guid RunId,
    EligibilityResult Eligibility,
    ProcedureResult Procedure,
    DraftResponse Draft,
    bool RequiresOfficerApproval);
using System;
using System.Collections.Generic;
using System.Text;

namespace DomainCopilot.Application.DTOs;

public sealed record DraftResponse(
    string ResponseText,
    IReadOnlyCollection<Citation> Citations,
    bool RequiresOfficerApproval);
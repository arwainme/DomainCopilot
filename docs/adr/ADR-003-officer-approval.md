
ADR-003: Officer Approval Gate
Status

Accepted

Context

Government responses can create an official statement about eligibility, obligations, required documents, fees, or timelines. The workflow therefore needs a human review step before final completion.

Decision

Government workflow runs enter WaitingForApproval after agent processing and before final approval.

Officers may:

approve
reject
edit and approve

Approval actions are persisted and audited.

Consequences

Positive:

adds human oversight before an official response is finalized
supports correction without restarting the full workflow
creates an auditable decision point

Trade-off:

the workflow is not fully automatic
the final user-facing result can depend on officer action

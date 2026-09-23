# Agentic Workflow

## 1. Overview

DomainCopilot implements the D4 Government workflow as an orchestrated sequence of specialized agents and supporting tools.

The workflow is designed to transform a citizen situation into an evidence-grounded draft response and then pause for officer approval.

## 2. Workflow

```text
Citizen Situation
        |
        v
Government Workflow Orchestrator
        |
        +--> Eligibility Identifier
        |       |
        |       +--> check_eligibility
        |
        +--> Procedure Resolver
        |       |
        |       +--> search_evidence
        |       +--> resolve_procedure
        |
        +--> Response Drafter
                |
                +--> evidence-backed draft
                        |
                        v
                 Approval Gate
                  /    |     \
             Approve Reject Edit
                  \    |     /
                       v
                 Persisted Run
                       |
                  Audit + Replay
3. Specialized Agents
Eligibility Identifier

Purpose:

identify the likely government service
assess eligibility from available evidence
identify ambiguity or missing information

Primary supporting capability:

check_eligibility

Procedure Resolver

Purpose:

determine the relevant procedure
retrieve supporting evidence
identify required documents, fees, and timelines

Supporting capabilities:

search_evidence

resolve_procedure

Response Drafter

Purpose:

transform the workflow findings into a grounded response
preserve evidence references
avoid unsupported definitive claims
4. Orchestration

The Government Workflow coordinates the specialized agents in a deterministic application workflow.

The orchestrator is responsible for:

creating and persisting a run
passing the citizen situation between stages
collecting evidence
coordinating agent outputs
creating the approval gate
recording audit events
exposing the final run state
5. Tools

Current tools include:

search_evidence
check_eligibility
resolve_procedure
submit_for_approval

submit_for_approval represents the write-oriented transition into the human approval stage.

6. Human-in-the-Loop

The workflow does not treat a generated government response as final immediately.

The run enters:

WaitingForApproval

The officer can:

approve
reject
edit and approve

Approval actions are persisted and audited.

7. Ambiguity and Safety

When available evidence is insufficient or the citizen situation is ambiguous, the system is designed to avoid inventing government obligations or entitlements.

The response should remain grounded in retrieved evidence and can require human review before completion.

8. Provider Resilience

LLM access is abstracted behind ILlmProvider.

The configured runtime can use:

OpenRouter -> Gemini fallback

Provider-specific failures before any streamed output can trigger fallback behavior in the selector.

9. Retrieval in the Workflow

The workflow uses hybrid retrieval:

0.45 lexical + 0.55 semantic

Semantic retrieval uses persisted embeddings associated with document chunks.

Evidence metadata is retained so generated responses can reference the supporting source.

10. Audit and Replay

Each workflow execution has a run identifier.

The run stores execution information and can be inspected after completion or while waiting for approval.

Replay creates a new execution derived from a previous run while preserving the original run history.

11. Example

Input:

What documents are required for the government service?

The workflow:

receives the citizen situation
identifies the relevant service/eligibility context
retrieves evidence
resolves procedure details
drafts a grounded response
submits the run for officer approval
persists the approval decision and audit information

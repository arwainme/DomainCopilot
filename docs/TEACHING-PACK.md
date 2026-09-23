# DomainCopilot Teaching Pack

## 1. Session Overview

### Title
DomainCopilot — Building an Agentic RAG Government Assistant

### Audience
Junior developers, software engineering trainees, and students familiar with C#, REST APIs, and basic software architecture.

### Suggested Duration
60–90 minutes.

## 2. Learning Objectives

By the end of the session, learners should be able to explain:

- what RAG is
- why embeddings are used
- how hybrid retrieval works
- how specialized agents can be orchestrated
- why human approval is useful in high-impact workflows
- how provider fallback works
- how audit and replay improve traceability
- how JWT authorization protects API endpoints

## 3. Architecture

```text
Client
  |
  v
API
  |
  v
Application / Workflow
  |
  +--> Eligibility Agent
  |
  +--> Procedure Agent
  |
  +--> Response Drafter
  |
  +--> Retrieval
  |      |
  |      +--> Lexical Search
  |      +--> Semantic Search
  |
  +--> Approval Gate
  |
  +--> Audit / Replay
  |
  v
SQL Server

LLM Providers:
OpenRouter -> Gemini fallback
4. Key Concepts
RAG

Retrieval-Augmented Generation combines retrieved evidence with language-model generation.

The system does not rely only on the model's internal knowledge. It first retrieves relevant evidence from the indexed corpus.

Embeddings

An embedding converts text into a numerical vector.

Similar meanings tend to produce vectors that are close in vector space.

DomainCopilot persists embeddings with document chunks.

Hybrid Retrieval

The current retrieval combines lexical and semantic signals:

Final Score =
0.45 × Lexical Score
+
0.55 × Semantic Score

This balances exact keyword matching with semantic similarity.

Agentic Workflow

Instead of asking one model to perform every task, the workflow assigns focused responsibilities:

Eligibility Identifier
        ?
Procedure Resolver
        ?
Response Drafter
        ?
Officer Approval
Human-in-the-Loop

The generated response is not immediately treated as the final official answer.

The workflow enters:

WaitingForApproval

The officer can:

approve
reject
edit and approve
Provider Fallback

The provider selector allows a primary provider and a fallback provider.

Current demonstrated configuration:

Primary: OpenRouter
Fallback: Gemini

The goal is resilience when a provider is unavailable.

5. Demonstration Flow
Step 1 — Login

Use the local demonstration account:

Username: officer
Password: officer123
Step 2 — Check Health
GET /health

Expected:

{
  "status": "healthy"
}
Step 3 — Check Available Tools
GET /api/tools

Expected tools include:

search_evidence
check_eligibility
resolve_procedure
submit_for_approval
Step 4 — Ask a Question

Example:

What documents are required for the government service?

Show the retrieved evidence and the grounded response.

Step 5 — Run Government Workflow
POST /api/workflows/government/execute

Example:

{
  "situation": "What documents are required for the government service?"
}

Explain how the workflow moves through the specialized agents.

Step 6 — Approval

Demonstrate:

WaitingForApproval
        ?
Edit and Approve

Explain that the officer remains responsible for the final decision.

Step 7 — Inspect the Run
GET /api/runs/{runId}

Show the persisted steps and status.

Step 8 — Replay
POST /api/runs/{runId}/replay

Explain that replay creates a new execution while preserving the original run.

Step 9 — Streaming
GET /api/llm/stream?prompt=Hello

Expected SSE-style output:

data: Hello
6. Hands-On Lab
Lab Task 1 — Retrieval

Ask three questions:

A question directly answered by the corpus.
A question using different wording for the same concept.
A question not supported by the corpus.

Observe:

retrieved evidence
citations
grounded answer
refusal/uncertainty behavior
Lab Task 2 — Workflow

Execute one government workflow and identify:

citizen input
eligibility step
procedure step
evidence retrieval
response drafting
approval gate
Lab Task 3 — Replay

Replay the run and compare:

original run ID
replayed run ID
execution steps
persisted status
7. Discussion Questions
Why is retrieval useful even when a language model can generate an answer directly?
What is the difference between semantic retrieval and keyword retrieval?
Why might a government workflow require human approval?
What should happen when the retrieved evidence is insufficient?
Why is provider fallback useful?
Why should workflow executions be persisted?
What security risks exist when an LLM processes user-controlled input?
8. Mini Assessment
Question 1

What is the main purpose of RAG?

Expected idea:

Retrieve relevant evidence and use it to ground generation.

Question 2

Why use hybrid retrieval?

Expected idea:

Combine exact lexical matching with semantic similarity.

Question 3

Why does the workflow stop at an approval gate?

Expected idea:

Allow a human officer to review the generated government response before finalization.

Question 4

What is replay?

Expected idea:

Run a previous workflow scenario again while retaining the original execution history.

Question 5

Why should API keys not be committed to Git?

Expected idea:

Prevent unauthorized use and credential exposure.

9. Suggested Teaching Sequence
10 min  — Problem and RAG introduction
10 min  — Architecture
15 min  — Retrieval and embeddings
15 min  — Agentic workflow
10 min  — Approval / audit / replay
10 min  — Live API demonstration
15 min  — Hands-on lab
10. Teaching Notes

Keep the explanation focused on the difference between:

Traditional CRUD/API
        versus
RAG
        versus
Agentic RAG

Emphasize that an agentic system is not simply "one prompt to an LLM."

The important engineering concepts are:

decomposition into specialized responsibilities
tool usage
state and persistence
provider abstraction
evidence grounding
human approval
observability and auditability
11. Takeaway

The key architecture lesson is:

Reliable AI Application
=
Models
+
Evidence
+
Orchestration
+
Controls
+
Persistence
+
Testing


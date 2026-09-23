# DomainCopilot System Design

## 1. Purpose

DomainCopilot is an Agentic RAG platform for government-service assistance.

The D4 Government workflow accepts a citizen situation, identifies the relevant service and eligibility, resolves required procedures/documents/fees/timelines, and produces an official response that must pass an officer approval gate before completion.

## 2. Architecture

The solution follows Clean Architecture:

- Domain: entities, enums, and business rules.
- Application: use cases, workflows, agents, services, DTOs, and abstractions.
- Infrastructure: persistence, LLM providers, retrieval, embeddings, audit, and external integrations.
- API: HTTP endpoints, authentication, authorization, SSE streaming, and middleware.

Dependency direction:

API -> Application -> Domain
Infrastructure -> Application -> Domain

## 3. Main Components

### API

Exposes:

- Authentication
- Document ingestion
- Government workflow execution
- Run inspection and replay
- Approval actions
- LLM streaming
- Tool discovery
- Usage inspection

A correlation-id middleware attaches a request correlation identifier to responses.

### Application

The main orchestration is the Government Workflow.

Specialized agents include:

- Eligibility Identifier
- Procedure Resolver
- Response Drafter

The workflow coordinates retrieval and tool calls and stops before final completion when officer approval is required.

### Retrieval

The retrieval layer combines:

1. lexical / keyword matching
2. semantic similarity over persisted embeddings

The current fusion is:

`finalScore = 0.45 * lexicalScore + 0.55 * semanticScore`

Retrieved evidence is returned with source/chunk metadata for citation.

### Persistence

SQL Server is used as the relational persistence layer.

EF Core migrations manage the database schema.

Documents and chunks are persisted in SQL Server. Chunk embeddings are persisted with the document chunks and used by application-side cosine similarity for semantic retrieval.

This is intentionally an MVP vector-storage approach rather than a dedicated external vector database.

### LLM Providers

The application uses an abstraction (`ILlmProvider`) covering:

- completion
- streaming
- tool calls
- embeddings

Current configured providers:

- OpenRouter
- Gemini
- OpenAI-compatible provider

Gemini is also used for the current embedding path so the indexed corpus remains in one embedding space.

### Approval

Government workflow runs enter a waiting-for-approval state before the final official response is committed.

Officer actions include:

- approve
- reject
- edit-and-approve

Approval actions are audited.

### Audit and Replay

Run information is persisted and can be inspected later.

Replay starts a new run based on a previous run identifier and keeps the original run available for auditability.

## 4. Request Flow

```text
Client
  |
  v
API
  |
  v
Government Workflow
  |
  +--> Eligibility Identifier
  |
  +--> Procedure Resolver
  |       |
  |       +--> Retrieval
  |              |
  |              +--> SQL persisted chunks
  |              +--> embeddings
  |
  +--> Response Drafter
  |
  v
Officer Approval Gate
  |
  +--> Approve
  +--> Reject
  +--> Edit and Approve
  |
  v
Persisted Run / Audit
5. Resilience

Provider selection supports a primary/fallback model.

If the primary completion or streaming provider fails before output is produced, the fallback provider is used.

Document embedding ingestion also supports batching, cancellation, and retry/backoff for rate-limit failures.

6. Security

The API uses JWT authentication with role-based authorization.

Sensitive endpoints are protected server-side.

Security controls and prompt-injection considerations are documented in docs/SECURITY.md.

Secrets are supplied through environment/configuration and are not stored in source control.

7. Observability

The system records:

correlation identifiers
run identifiers
provider/model usage
token usage where returned by the provider
estimated usage cost where applicable
persisted run and audit information

Health checking is available through /health.

8. Known MVP Boundaries
SQL Server stores embeddings instead of a dedicated vector database.
Local Ollama is supported by the provider abstraction but is not required for the demonstrated deployment.
OpenAI-compatible providers remain configurable alternatives; the demonstrated zero-cost path uses Gemini/OpenRouter.

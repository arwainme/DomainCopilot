# DomainCopilot — Agentic RAG Platform

DomainCopilot is an Agentic RAG platform designed for government-service assistance.

The system combines document ingestion, hybrid retrieval, specialized agents, LLM provider fallback, human approval, audit/replay, streaming, authentication, and persistent run history.

## Project Goal

The D4 Government workflow transforms a citizen's situation into a grounded government-service response:

```text
Citizen Situation
       ↓
Service / Eligibility Identification
       ↓
Procedure Resolution
       ↓
Required Documents / Fees / Timelines
       ↓
Response Drafting
       ↓
Officer Approval
       ↓
Final Response + Audit
```

The system is designed to reduce unsupported claims by grounding responses in indexed evidence and requiring officer approval before final completion.

---

## Main Features

### Agentic Government Workflow

The platform contains specialized agents:

* **Eligibility Identifier**
* **Procedure Resolver**
* **Response Drafter**

The workflow orchestrates these agents and the supporting tools.

### RAG and Hybrid Retrieval

Documents are ingested, cleaned, chunked, embedded, and indexed.

Retrieval combines:

```text
45% Lexical Score
+
55% Semantic Score
=
Final Retrieval Score
```

Retrieved evidence keeps document and chunk metadata so generated responses can provide source citations.

The current MVP stores embeddings with document chunks in SQL Server and performs cosine-similarity retrieval in the application.

### Document Ingestion

Supported formats:

* PDF
* TXT

The ingestion pipeline provides:

* text extraction
* text cleaning
* deterministic document IDs
* chunk creation
* embedding generation
* persisted chunks
* persisted embeddings
* idempotent re-ingestion
* processing/completed/failed document states
* batching and retry/backoff for embedding rate limits

Current demonstration corpus:

```text
30 documents
972 persisted chunks
0 failed documents
```

### Human Approval

Government workflow runs stop at an approval gate.

Officer actions:

```text
Approve
Reject
Edit and Approve
```

Approval actions are persisted and audited.

### Audit and Replay

Runs are persisted with run identifiers and execution information.

A previous run can be replayed using its run ID, producing a new run while preserving the original history.

### LLM Provider Abstraction

LLM access is abstracted behind `ILlmProvider`.

The abstraction supports:

* completion
* streaming
* tool calls
* embeddings

Current providers:

```text
OpenRouter
Gemini
OpenAI-compatible Provider
Local Provider
```

The demonstrated zero-cost runtime configuration uses:

```text
Primary: OpenRouter
Fallback: Gemini
```

Gemini is used for the current embedding path so indexed documents remain in one embedding space.

### Streaming

The API exposes Server-Sent Events (SSE) for token/chunk streaming.

Example:

```text
data: Hello

data: it's nice to meet you!
```

Streaming uses cancellation tokens propagated from the HTTP request.

### Authentication and Authorization

The API uses JWT authentication and role-based authorization.

The demonstration application includes an Officer role.

Example demo credentials:

```text
Username: officer
Password: officer123
```

These credentials are for local demonstration only and must not be used in production.

### Observability

The system records:

* correlation IDs
* run IDs
* provider/model information
* token usage where available
* estimated usage cost where applicable
* persisted run history
* audit events

Health endpoint:

```http
GET /health
```

---

## Architecture

The project follows Clean Architecture.

```text
DomainCopilot
│
├── src
│   ├── DomainCopilot.Domain
│   ├── DomainCopilot.Application
│   ├── DomainCopilot.Infrastructure
│   └── DomainCopilot.API
│
└── tests
    ├── DomainCopilot.Domain.Tests
    └── DomainCopilot.Integration.Tests
```

### Domain

Contains:

* entities
* enums
* domain rules

### Application

Contains:

* use cases
* workflows
* agents
* DTOs
* abstractions
* retrieval services
* ingestion services
* approval logic

### Infrastructure

Contains:

* SQL Server persistence
* EF Core
* LLM providers
* embedding providers
* retrieval implementation
* audit persistence
* external integrations

### API

Contains:

* HTTP controllers
* JWT authentication
* authorization
* middleware
* SSE streaming
* health endpoints

Dependency direction:

```text
API
 ↓
Application
 ↓
Domain

Infrastructure
 ↓
Application
 ↓
Domain
```

---

## C4 and Architecture Documentation

Architecture documentation is available under:

```text
docs/
├── SYSTEM-DESIGN.md
├── C4.md
├── SECURITY.md
└── ADR/
    ├── ADR-001-llm-provider-abstraction.md
    ├── ADR-002-sql-vector-persistence.md
    ├── ADR-003-officer-approval.md
    └── ADR-004-hybrid-retrieval.md
```

---

## API Endpoints

### Authentication

```http
POST /api/auth/login
```

### Document Ingestion

```http
POST /api/documents/ingest
```

Multipart form upload.

Supported files:

```text
.pdf
.txt
```

### Government Workflow

```http
POST /api/workflows/government/execute
```

Example:

```json
{
  "situation": "What documents are required for the government service?"
}
```

### Run Inspection

```http
GET /api/runs/{runId}
```

### Replay

```http
POST /api/runs/{runId}/replay
```

### Approval

```http
POST /api/runs/{runId}/approve
POST /api/runs/{runId}/reject
POST /api/runs/{runId}/edit-and-approve
```

### LLM Streaming

```http
GET /api/llm/stream?prompt=Hello
```

### Tools

```http
GET /api/tools
```

### Usage

```http
GET /api/usage
```

### Health

```http
GET /health
```

---

## Government Workflow

The main workflow performs:

```text
1. Receive citizen situation
2. Identify relevant service / eligibility
3. Retrieve supporting evidence
4. Resolve procedure
5. Identify documents / fees / timelines
6. Draft grounded response
7. Persist workflow run
8. Wait for officer approval
9. Approve / reject / edit-and-approve
10. Persist audit information
```

If the evidence is insufficient or the situation is ambiguous, the workflow can avoid making an unsupported definitive claim and escalate for review.

---

## Tools

The current workflow exposes specialized tools including:

```text
search_evidence
check_eligibility
resolve_procedure
submit_for_approval
```

Write-like actions are protected by the approval flow.

---

## Data Persistence

The application uses SQL Server as its relational database.

EF Core migrations manage schema changes.

Main persisted concepts include:

```text
Documents
DocumentChunks
Runs
Approvals
Audit Events
Usage Information
```

Embeddings are persisted with document chunks.

Current MVP vector retrieval is implemented by combining lexical retrieval with cosine similarity over persisted embeddings.

A dedicated external vector database is intentionally not required for the current MVP.

---

## Database Migration

Apply migrations with:

```powershell
dotnet ef database update `
  --project ".\src\DomainCopilot.Infrastructure" `
  --startup-project ".\src\DomainCopilot.API"
```

Migration history is stored through EF Core.

---

## Running Locally

### Requirements

* .NET 10 SDK
* SQL Server
* Git

Optional:

* Docker Desktop
* OpenRouter API key
* Gemini API key
* OpenAI-compatible API key

### Configuration

Sensitive credentials should be supplied through environment variables.

Example:

```powershell
$env:LlmProvider__PrimaryProvider = "OpenRouter"
$env:LlmProvider__FallbackProvider = "Gemini"
$env:LlmProvider__OpenRouter__ApiKey = "YOUR_KEY"
$env:Gemini__ApiKey = "YOUR_KEY"
```

Do not commit secrets to Git.

### Build

```powershell
dotnet build ".\DomainCopilot.slnx"
```

### Test

```powershell
dotnet test ".\DomainCopilot.slnx"
```

### Run

```powershell
dotnet run `
  --project ".\src\DomainCopilot.API\DomainCopilot.API.csproj"
```

The API listens on:

```text
http://localhost:5035
```

---

## Docker Compose

Docker Compose configuration is included for containerized execution.

```text
docker-compose.yml
```

The compose setup includes the application and SQL Server dependencies.

Docker is not required for normal local development when SQL Server and the .NET SDK are available locally.

---

## Testing

The solution contains unit and integration tests.

Run all tests:

```powershell
dotnet test ".\DomainCopilot.slnx"
```

The evaluation harness covers retrieval and response behavior, including adversarial questions and refusal scenarios.

Important evaluation dimensions include:

```text
Retrieval Hit Rate
Citation Coverage
Groundedness
Refusal Correctness
Adversarial Safety
```

Historical baseline results are documented separately from the final post-refactor measurements.

---

## Security

Security controls and threat considerations are documented in:

```text
docs/SECURITY.md
```

The security documentation covers areas including:

* authentication
* authorization
* secret management
* prompt injection
* input validation
* sensitive endpoint protection
* logging considerations
* LLM-specific risks
* secret scanning

Secrets must remain outside source control.

---

## CI

GitHub Actions is configured under:

```text
.github/workflows/ci.yml
```

The CI pipeline performs automated restore, build, testing, and dependency/security checks.

The repository also uses protected branch rules and GitHub issue/milestone tracking for assessment work.

---

## Project Status

Core platform capabilities currently demonstrated:

```text
✅ Clean Architecture
✅ Government agentic workflow
✅ Real PDF/TXT ingestion
✅ Idempotent ingestion
✅ 30-document corpus
✅ 972 persisted chunks
✅ Persisted embeddings
✅ Hybrid retrieval
✅ Exact evidence metadata/citations
✅ Specialized agents
✅ Tool-based orchestration
✅ Officer approval
✅ Edit-and-approve
✅ Audit persistence
✅ Run replay
✅ JWT authentication
✅ Role-based authorization
✅ SSE streaming
✅ Multiple LLM providers
✅ SQL Server persistence
✅ EF Core migrations
✅ Unit tests
✅ Integration tests
✅ Security documentation
✅ GitHub Actions
✅ Branch protection
```

Remaining delivery work includes final documentation polish, teaching material, deployment verification, final evaluation refresh, and fresh-clone verification.

---

## Known MVP Boundaries

The following are deliberate MVP boundaries:

1. Embeddings are stored in SQL Server rather than a dedicated external vector database.

2. Local Ollama support exists in the provider abstraction but is not required for the demonstrated zero-cost configuration.

3. OpenAI-compatible integration is optional. The demonstrated runtime path uses OpenRouter and Gemini.

4. Some provider-specific features may differ between implementations and are normalized behind the common provider abstraction.

---

## Repository Structure

```text
DomainCopilot/
│
├── .github/
│   └── workflows/
│       └── ci.yml
│
├── docs/
│   ├── SECURITY.md
│   ├── SYSTEM-DESIGN.md
│   ├── C4.md
│   └── ADR/
│
├── data/
│   └── documents/
│
├── src/
│   ├── DomainCopilot.Domain/
│   ├── DomainCopilot.Application/
│   ├── DomainCopilot.Infrastructure/
│   └── DomainCopilot.API/
│
├── tests/
│   ├── DomainCopilot.Domain.Tests/
│   └── DomainCopilot.Integration.Tests/
│
├── Dockerfile
├── docker-compose.yml
├── DomainCopilot.slnx
└── README.md
```

---

## Development Principles

The project follows:

* Clean Architecture
* SOLID principles
* dependency inversion
* typed contracts
* provider abstraction
* human-in-the-loop approval
* evidence-grounded generation
* explicit auditability
* secure secret handling
* automated testing
* incremental Git-based development

---

## License

This project was created as part of an ITI technical instructor assessment and is intended for educational and evaluation purposes.

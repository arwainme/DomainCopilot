# DomainCopilot

DomainCopilot is an Agentic RAG platform for the D4 Government domain.

## Main Features

- PDF and TXT document ingestion
- Document and chunk metadata
- Idempotent document ingestion
- Persisted embeddings
- Hybrid keyword + semantic retrieval
- Exact evidence citations
- Eligibility Identifier agent
- Procedure Resolver agent
- Response Drafter agent
- Government workflow orchestrator
- Human officer approval
- Approve / Reject / Edit-and-Approve
- Persisted audit/run trace
- Run replay
- JWT authentication and role-based authorization
- Streaming endpoint
- Correlation IDs
- Usage tracking
- SQL Server + EF Core
- Docker Compose configuration
- GitHub Actions CI
- Security controls

## Architecture

The system uses Clean/Onion Architecture.

```text
API
 |
 v
Application
 |
 +--> Domain

Infrastructure
 |
 +--> SQL Server
 +--> LLM Providers
The Application layer depends on abstractions rather than concrete LLM or database SDKs.

Government Workflow
Citizen question
      |
      v
Retrieve evidence
      |
      v
Eligibility Identifier
      |
      v
Procedure Resolver
      |
      v
Response Drafter
      |
      v
Officer Approval
      |
      v
Final response
Run the API
dotnet run --project .\src\DomainCopilot.API\DomainCopilot.API.csproj

Current development URL:

http://localhost:5035
Demo Login
username: officer
password: officer123

These credentials are for local demonstration only.

Execute Government Workflow
$login = Invoke-RestMethod `
  -Uri "http://localhost:5035/api/auth/login" `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"username":"officer","password":"officer123"}'

$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }

$run = Invoke-RestMethod `
  -Uri "http://localhost:5035/api/workflows/government/execute" `
  -Method Post `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{"situation":"What documents are required for the government service?"}'

$run

Inspect the run:

Invoke-RestMethod `
  -Uri "http://localhost:5035/api/runs/$($run.runId)" `
  -Headers $headers `
  -Method Get
Ingestion

Example:

$pdf = Get-Item ".\data\documents\government-service-03.pdf"

curl.exe -X POST "http://localhost:5035/api/documents/ingest" `
  -H "Authorization: Bearer $token" `
  -F "file=@$($pdf.FullName)"
Retrieval

The system persists chunk embeddings and reuses them during retrieval.

Hybrid ranking combines lexical and semantic similarity:

finalScore = 0.45 * lexicalScore + 0.55 * semanticScore
Database

The relational store uses SQL Server and EF Core migrations.

dotnet ef database update `
  --project .\src\DomainCopilot.Infrastructure `
  --startup-project .\src\DomainCopilot.API
Testing
dotnet build "DomainCopilot.slnx"
dotnet test "DomainCopilot.slnx"

Current verified test result:

14 tests passed
Security

Security controls and OWASP-oriented protections are documented in:

docs/SECURITY.md

Evaluation

The evaluation dataset is:

questions.json

Evaluation documentation:

docs/EVALUATION.md

Known MVP Gaps
Only a subset of the generated corpus is currently indexed because of hosted embedding quota limits.
A second independently verified runtime LLM provider is not currently configured.
True end-to-end server-side cancellation needs final verification.
A dedicated minimal UI/CLI remains to be completed.
Docker runtime has not been verified on the current development machine.
Teaching materials and videos remain deliverables.
Project Structure
src/
  DomainCopilot.API
  DomainCopilot.Application
  DomainCopilot.Domain
  DomainCopilot.Infrastructure

tests/
  DomainCopilot.Domain.Tests
  DomainCopilot.Integration.Tests

docs/
  SECURITY.md


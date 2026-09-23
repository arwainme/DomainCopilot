
AI Usage Log
Purpose

This document records how AI-assisted development was used during DomainCopilot implementation.

AI assistance was used as a development aid, not as a substitute for testing, source review, or engineering decisions.

Development Uses
Code Assistance

AI assistance was used for:

generating initial code structures
suggesting implementation approaches
refactoring repetitive code
diagnosing compiler/runtime errors
improving error handling
drafting tests
suggesting documentation structure

All generated or suggested code was reviewed, adapted, built, and tested in the project environment.

Debugging Assistance

AI assistance was used to help interpret:

.NET build errors
dependency injection failures
HTTP/API failures
embedding quota errors
provider fallback behavior
authentication failures
PowerShell command issues

Actual application output and logs were used to validate fixes.

Architecture Assistance

AI assistance contributed suggestions related to:

Clean Architecture boundaries
provider abstraction
agent orchestration
hybrid retrieval
approval workflow
audit/replay design
documentation structure

Final architectural choices were adapted to the assessment requirements and the actual implementation.

Documentation Assistance

AI assistance was used to draft and improve:

README
BRD
evaluation documentation
system design
C4 diagrams
ADRs
security documentation
agentic workflow documentation

Project-specific details were checked against the implemented code and observed runtime behavior.

Verification Principle

AI-generated suggestions were not treated as authoritative.

Verification was performed through one or more of:

source-code inspection
dotnet build
dotnet test
API execution
database queries
provider HTTP responses
Git status/history
manual workflow testing
External AI Providers

The application itself supports multiple LLM providers through ILlmProvider.

The demonstrated zero-cost runtime configuration uses:

OpenRouter as the primary completion/streaming provider
Gemini as fallback and current embedding provider

OpenAI-compatible integration remains an optional configured provider.

Sensitive Information

API keys, passwords, and other credentials were not intended to be stored in source control.

Secrets should be supplied through environment variables or local secret configuration.

Human Responsibility

The project author remains responsible for:

reviewing generated code
validating behavior
checking assessment requirements
testing changes
maintaining repository history
making final engineering decisions

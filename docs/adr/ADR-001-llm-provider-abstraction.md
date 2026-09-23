
ADR-001: LLM Provider Abstraction
Status

Accepted

Context

The platform must support multiple LLM providers and a fallback path without coupling application workflows to a single vendor.

Decision

Define ILlmProvider in the Application layer and implement provider adapters in Infrastructure.

The abstraction covers:

completion
streaming
tool calls
embeddings

A provider selector chooses the configured primary provider and fallback provider.

Consequences

Positive:

workflows remain provider-independent
providers can be replaced without changing application logic
fallback behavior is centralized
provider-specific HTTP code stays in Infrastructure

Trade-off:

provider capabilities are not perfectly identical and adapter code must normalize differences.

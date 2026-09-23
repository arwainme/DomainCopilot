# Security

## Scope

DomainCopilot is an agentic RAG platform for government-service information. It processes public or synthetic documents and must not be used as a source of legal advice or as a substitute for official government decisions.

## Authentication and Authorization

- API authentication uses JWT bearer tokens.
- Officer-only approval operations require the `Officer` role.
- Approval actions are server-side protected.
- Credentials and API keys must be supplied through environment variables or local secret configuration.
- Secrets must never be committed to source control.

## OWASP Web Top 10 Controls

### Broken Access Control
- Authorization is enforced server-side using ASP.NET Core authorization policies and roles.
- Approval endpoints require the `Officer` role.

### Cryptographic Failures
- Secrets are not stored in source code.
- Production deployments should use HTTPS and managed secret storage.

### Injection
- Database access uses Entity Framework Core parameterized queries.
- User-provided text is treated as untrusted input.

### Insecure Design
- High-risk government responses require human officer approval.
- The workflow includes bounded iterations and explicit agent responsibilities.

### Security Misconfiguration
- Configuration is externalized.
- Sensitive configuration values are not stored in the repository.

### Vulnerable Components
- Dependencies should be regularly checked and updated.
- CI should run dependency vulnerability scanning.

### Identification and Authentication Failures
- Protected API operations require authentication.
- Passwords and API credentials must not be hard-coded in production.

### Software and Data Integrity Failures
- CI should validate builds and tests before changes are merged.
- Dependency and secret scanning should run in CI.

### Logging and Monitoring Failures
- Workflow steps and officer approval decisions are persisted in the audit trail.
- Run identifiers provide correlation across workflow activity.

### Server-Side Request Forgery
- External integrations must use explicit allowlists and validated endpoints.
- Agents must not be allowed to freely access arbitrary URLs.

## OWASP LLM Top 10 Controls

### Prompt Injection
Direct and indirect prompt injection are treated as untrusted input.

Controls:
- Retrieved documents are treated as evidence, not instructions.
- Agent prompts explicitly separate system instructions from retrieved content.
- Retrieved content must not override agent policies.
- Tool execution is restricted to explicitly allowed tools.
- High-risk side effects require officer approval.

Example adversarial inputs include:
1. A document containing instructions pretending to be system messages.
2. A citizen query attempting to override retrieval or approval rules.
3. Retrieved content instructing an agent to disclose secrets.

### Sensitive Information Disclosure
- API keys and credentials must never be included in prompts or responses.
- Public/synthetic documents are used for the evaluation corpus.
- Sensitive data should be redacted before being exposed to models or users.

### Excessive Agency
- Agents have restricted responsibilities.
- Tools are explicitly registered and allowlisted.
- Side-effecting approval operations require human authorization.

### Insecure Output Handling
- LLM output is treated as untrusted.
- Structured application contracts are used between agents.
- Government responses must include supporting evidence and citations.

### Unbounded Consumption
- Agent workflows use bounded iterations.
- Requests should use cancellation and timeouts.
- Retrieval uses bounded result sets.

### Supply Chain
- External model providers are accessed through the `ILlmProvider` abstraction.
- Provider credentials are external configuration.
- Dependencies should be scanned in CI.

## Prompt Injection Test Cases

The evaluation set includes adversarial cases covering:
- Direct instruction override.
- Indirect instructions embedded in retrieved documents.
- Attempts to bypass officer approval.
- Attempts to obtain credentials or internal system information.
- Unsupported claims designed to make the system invent government obligations.

Expected behavior:
- Ignore malicious instructions.
- Follow system and workflow policies.
- Ground factual responses in retrieved evidence.
- Refuse or escalate when evidence is insufficient.
- Never expose secrets.

## Secret Management

Never commit:
- API keys
- JWT signing secrets
- Database passwords
- OAuth client secrets
- Provider credentials

Local development should use environment variables or user secrets.

Before publishing a repository, perform a full-history secret scan.

## Incident Response

If a credential is accidentally committed:
1. Revoke or rotate the credential immediately.
2. Remove it from the working tree.
3. Remove it from Git history if necessary.
4. Verify the replacement credential is stored securely.
5. Perform a repository-wide secret scan.

## Security Verification Checklist

- [ ] JWT authentication enabled
- [ ] Officer authorization enforced
- [ ] Approval operations require authorization
- [ ] Secrets externalized
- [ ] Prompt injection cases included in evaluation
- [ ] Tool allowlists enforced
- [ ] Human approval required for side effects
- [ ] Bounded agent iterations
- [ ] Request cancellation supported
- [ ] Dependency scanning enabled
- [ ] Secret scanning enabled
- [ ] Full Git history scanned before final submission

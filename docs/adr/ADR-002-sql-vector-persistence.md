
ADR-002: SQL Server for Relational and Vector Persistence
Status

Accepted

Context

The assessment requires relational persistence, migrations, and vector-based retrieval.

A dedicated external vector database would add deployment and operational complexity for the MVP.

Decision

Use SQL Server for:

relational entities
documents
document chunks
persisted embeddings
run and audit persistence

Semantic retrieval calculates cosine similarity in the application over persisted embeddings and combines it with lexical retrieval.

Consequences

Positive:

one persistence dependency
simple local development
EF Core migrations cover the relational schema
embeddings survive application restarts

Trade-off:

this is not a dedicated vector database
semantic search is less scalable than a specialized vector engine for a much larger corpus

This remains an explicit MVP boundary documented in the system design.

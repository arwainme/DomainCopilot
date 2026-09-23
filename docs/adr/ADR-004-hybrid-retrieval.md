
ADR-004: Hybrid Retrieval
Status

Accepted

Context

Pure keyword retrieval can miss semantic matches, while pure semantic retrieval can weaken exact lexical matching for important government terms.

Decision

Use hybrid retrieval combining lexical and semantic scores.

Current fusion:

0.45 lexical + 0.55 semantic

The system also keeps exact source/chunk metadata so generated answers can cite the retrieved evidence.

Consequences

Positive:

improves recall across wording variations
preserves strong exact-term matching
keeps citations tied to persisted evidence

Trade-off:

requires both retrieval paths
score normalization and weighting require evaluation and tuning

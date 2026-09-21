# Phase 5 RAG threat model

**Latest September 11 controls:** new canonical sources are screened with
`cited-source-screen-v1` and suspicious indexes are quarantined. The generation
coordinator also rejects declared hostile source patterns before preparation or
reservation. Sixteen unit checks cover obfuscation, false labels, HTML, split
instructions and benign controls. Database checks verify quarantine and zero
reservation on poisoned prompt evidence. The
[development acceptance matrix](../evidence/phase-5/P5-development-acceptance-20260911.md)
maps these to the existing isolation, cache, tamper, lifecycle and inert-rendering
checks. General model answers remain disabled; these controls do not claim a
universal injection classifier. The preceding audit's missing scanner and split
coverage are now implemented for the declared development scope.

**September 11 audit:** [Current acceptance findings](../evidence/phase-5/P5-acceptance-audit-20260911.md)
add six reviewed-answer adversarial database checks: poisoned canonical page,
hostile questions, tampered facts/pages, active-actor ownership, stale zero-hit
corpus and forged/swapped citations. The deterministic route exposes no provider
or tools. These passes do not establish the planned ingestion/prompt scanner,
split-instruction coverage or model-path red-team acceptance. Those entries below
remain open rather than being inferred from prompt wording alone.

**Status:** Declared development controls implemented and verified; optional model and production controls remain scoped below
**Scope:** Cited questions over completed reviewed documents

Document text is untrusted data even when a user is authorized to view the
document. Retrieval does not promote embedded text into instructions.

## Assets and boundaries

Protected assets are source documents, page text, reviewed corrections,
embeddings, questions, answers, citations, provider credentials, and tenant
membership. Trust boundaries exist at upload/extraction, ASP.NET-to-Python
calls, Python-to-model calls, PostgreSQL retrieval, answer validation, browser
rendering, caches, and deletion/re-indexing.

Phase 5 proves structural organization isolation with the fixed development
identity. It does not claim that authentication, RBAC, retention, encryption,
or production tenant security are complete; those remain Phase 6 work.

## Threats and required controls

| Threat | Required Phase 5 controls | Verification |
|---|---|---|
| Cross-organization retrieval | Derive organization from execution context; authorize the frozen corpus in ASP.NET; apply organization/corpus predicates before both rankings; post-validate every hit and citation | Seed two organizations and prove zero foreign hits for lexical, vector, hybrid, cached, and citation-open paths |
| Indirect prompt injection in documents | Treat chunks as delimited data; scan at ingestion and again before prompt assembly; quarantine suspicious chunks; reinforce instructions; do not expose tools | Poisoned-document tests cannot change answer policy, disclose other context, or invoke an action |
| Corpus poisoning | Only completed reviewed revisions are eligible; record source/review/index versions; support quarantine and re-indexing | Unreviewed, superseded, or quarantined sources never enter an eligible corpus |
| Citation fabrication or swapping | Persist retrieved hit IDs; require claim-level citations; validate citation IDs and immutable source metadata server-side | Unknown, altered, foreign, or non-retrieved citations fail closed |
| Model-memory hallucination | Prompt forbids outside knowledge; deterministic output validator requires cited claims; explicit insufficient-evidence status | Unanswerable questions refuse without a factual answer |
| Context flooding and denial of service | Bound question length, corpus count, candidates, final chunks/tokens, timeouts, retries, and output size | Boundary and timeout tests return stable safe errors |
| Sensitive-data leakage through logs | Structured safe telemetry; no raw page text, full prompt, answer, credentials, or embeddings in normal logs | Log-capture tests and manual inspection |
| Cache leakage | Cache key includes organization, frozen corpus, question hash, retrieval/prompt/model versions; validate cached citations again | Same question in two organizations never shares an answer |
| Stale source after deletion or access change | Invalidate eligibility, chunks, embeddings, caches, and citation access from the canonical source state | Deletion/revocation scenario returns no stale hit |
| Unsafe rendered output | Render answer as text; sanitize labels; allow citation navigation only to an authorized internal route | Script/HTML and malicious filename tests |
| Cost abuse or provider failure | Per-request limits, nullable cost accounting, circuit breaker/timeout, and deterministic evidence-only fallback | Budget and provider-failure tests |

## Prompt and output rules

### Implemented workspace boundary (P5-T07 subset)

The evidence workspace renders filenames and source text as React text rather
than HTML/Markdown links. Its navigation targets are internal opaque-ID routes.
The server checks organization, actor, saved hit/corpus membership, current
index eligibility, pinned chunk identity, and exact canonical UTF-8 spans.
Source discovery and evidence-open responses are no-store. Reviewed-only
provenance does not manufacture a page quotation.

These controls are covered by component/API/database checks in the
[workspace checkpoint](../evidence/phase-5/P5-T07-workspace-verification.md).
They do not implement model prompt-injection defenses or complete this threat
model. Current eligibility checks are not atomic revocation/erasure of already
loaded browser data; saved retrieval history remains a snapshot. There are no
vector/model-answer paths enabled in this checkpoint.

### Remaining model prompt rules

The [database-log checkpoint](../evidence/phase-5/P5-T08-database-log-verification.md)
checks live PostgreSQL and private Python indexing/retrieval. EF exceptions can
contain private primary error text even with sensitive-data and error-detail
logging disabled; registered application contexts now replace error/exception
log diagnostics with fixed events. PostgreSQL's separate server log still
contained synthetic conversion input and conflicting keys. Its access,
retention, export/redaction and deployment policy gate remains **open**. Passing
application regressions does not make server logs private.

The [workflow-log checkpoint](../evidence/phase-5/P5-T08-workflow-log-verification.md)
extends capture to successful retrieval/history/evidence and controlled downstream
failures using in-memory persistence. It replaces the private cited client's
default HTTP logging with content-free telemetry (formatted header redaction
alone retained raw structured values on this runtime) and safely handles unexpected
endpoint failures. Real database logs and verbose URL/query metadata remain
explicit limitations, not passed release gates.

The subsequent [request-boundary checkpoint](../evidence/phase-5/P5-T08-request-boundary-verification.md)
adds a 64 KiB cited-body cap, bounded streaming reads, a 15-second read deadline,
safe JSON parsing failures, and captured-log tests for rejected synthetic
payloads. This is not a comprehensive logging audit or rate/concurrency limit;
the invoice-upload path and provider/model gates remain separate.

P5-T08 adds current-eligibility checks to public history and same-key replay,
including zero-hit corpora, without deleting stored audit history. All cited
responses now have a pre-binding no-store policy. Provider-free hostile text
and retirement/quarantine checks passed, along with one real-stack synthetic
native-PDF browser workflow. See the [security checkpoint](../evidence/phase-5/P5-T08-security-verification.md).
Text remains inert because no model/tools are exposed in this path; this is
not verification of the model prompt rules below or a complete red-team gate.

- Retrieved blocks carry opaque source IDs and explicit start/end delimiters.
- The model is told that source text can contain malicious instructions and must
  be used only as evidence.
- The model cannot choose organization filters or source IDs.
- The model produces a typed response, not trusted HTML or Markdown links.
- Server validation, not the model, constructs citation targets.
- No answer output is executed as code or forwarded to a tool.
- Prompt templates and validators are versioned and retained with each run.

## Minimum red-team set

The release gate includes direct and indirect “ignore previous instructions”
payloads, zero-width and obfuscated variants, split instructions across chunks,
false system/source labels, forged citation IDs, hostile filenames, cross-tenant
queries, cache reuse, oversized questions, conflicting sources, unsupported
legal/tax conclusions, and deletion after indexing.

This threat model is informed by the [OWASP RAG Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/RAG_Security_Cheat_Sheet.html),
the [OWASP prompt-injection guidance](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html),
and [NIST's adversarial machine-learning taxonomy](https://nvlpubs.nist.gov/nistpubs/ai/NIST.AI.100-2e2025.pdf).

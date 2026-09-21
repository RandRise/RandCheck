# Source context and excerpt binding, v4

The v3/low evaluation preserved correct amounts cited to the wrong fact, a
three-document claim supported by only two documents, and an incomplete
comparison. The private v4 candidate exposes document identity alongside each
hit and requires a literal supporting excerpt in each citation. It has not yet
been evaluated with generated model answers. Previous scores remain unchanged.

## Evidence packet

`CitedGenerationSettings.SourceAwarePilot` defaults to false and has no public
configuration or request switch. When explicitly selected internally, the .NET
coordinator sends source schema `1.1` with each authorized hit's index, document,
review and extraction IDs, filename, chunk kind, review action ID, and exact
page spans. Index identity and readiness are checked against the loaded chunk.
Filename is an untrusted display label, not document-type authority or a policy.
The page and reviewed-fact content stay distinct. Review corrections retain the
original machine quote without turning it into support for the corrected value.

The Python boundary rejects missing/mixed metadata, unknown fields, malformed
identities, span mismatches, invalid UTF-8 byte ranges and wrong page quotes.
All metadata participates in the prompt and request hashes. Transport rebuilds
and verifies the exact counting and generation bodies, including reasoning effort.
The existing byte, token, deadline and spend ceilings remain in effect.

V3/source schema `1.0` continues to produce its original bytes. There is no silent
upgrade of saved dispatches. The same historical run cannot be dispatched again
by changing the prompt flag; its saved request fails the source comparison.

## Answer contract

The v4 prompt uses answer schema `1.1`. Each citation contains the existing exact
`hitId`/`spanId` pair and required `supportText` of 1–1000 characters. The provider
schema retains a root object, nested status alternatives, required fields and
closed objects. These constraints follow the official
[Structured Outputs schema requirements](https://developers.openai.com/api/docs/guides/structured-outputs).

With v4 explicitly selected, `CitedAnswerValidator` reports
`citation-excerpt-binding-v2`. It requires an exact ordinal substring of the
referenced page span for `PageText`, or of reviewed content for `ReviewedFact`.
It rejects invented text, another span's text and an old machine value used as
support for a changed review value. Excerpts are not normalized or repaired.
Existing identity, duplicate-pair, graph, hash, scope and payment policies apply.
Known usage is retained even when an excerpt or schema downgrade is rejected.
Replay applies the same validation without remeasurement or redispatch.

The new prompt asks for each requested document and comparison operand, with
separate citations and explicit missing parts. This instruction is not an
automatic completeness test. Multiple hits from one document still represent
only one document.

## Limits and next evaluation

Literal membership proves text location, not relevance, entailment, arithmetic,
coverage, or resistance to instructions embedded in evidence. A deliberately
false claim with a real excerpt still passes membership validation in the tests.
`SemanticSupportEstablished` and `PublicationAllowed` remain false. Public
questions continue through the provider-free evidence workspace.

The offline audit rebuilds the saved v3 packets and constructs 30 v4 snapshots
against unchanged synthetic evidence. Those snapshots reuse historical IDs only
for comparison and are not an execution plan. A paid evaluation requires a new
frozen manifest and new retrieval/operation IDs, while reusing saved embeddings,
the nano snapshot and low reasoning. Keep the question references and historical
reports unchanged, retain every rejection and cost, and measure support and
comparison completeness separately. Verify the UTC rate-validity interval
before admitting new calls. No public generation follows from packet checks.

The execution manifest was frozen as `answer-source-v0.4.0`, with fresh retrieval
IDs and unchanged evidence and references. The user explicitly approved its
USD 0.06 synthetic run. Four cases were attempted before network failures stopped
the evaluation; 26 remain unrun. See [partial run and recovery state](../evidence/phase-5/P5-source-context-partial-run.md)
and [pre-run verification](../evidence/phase-5/P5-source-context-verification.md).

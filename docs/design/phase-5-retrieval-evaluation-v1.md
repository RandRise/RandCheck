# Frozen retrieval evaluation v0.1.0

Frozen before new paid calls/ranking on 2026-09-10. The existing30-question
dataset, expected answers, labeled page quotes and selected source sets are
unchanged. The prepared plan pins corpus/source/chunk/question hashes and Python
lexical plans. Algorithm settings remain lexical-or-English, exact cosine and
RRF60 with20 candidates per leg; all modes return at most5 hits. No synonym,
score-threshold, candidate-limit or label changes follow observed results.

## Scoring rules

For each answerable question, deduplicate its labeled evidence by source ID,
page number and exact quote. A label is retrieved when a hit from that source
has a page span containing the full exact quote on the labeled page. Require
every label to exist in the frozen canonical corpus before scoring. This strict
quote-coverage metric does not treat a shorter value-only span as the full label.

Report mean labeled-evidence recall at1 and5, and the fraction of questions
with all labels covered at5. Also report hit rate at1/5 and mean reciprocal rank
of the first hit covering any label (zero if absent). List every partial/missing
case and category totals. The primary95% retrieval gate is evaluated against
mean labeled-evidence recall at5; full-coverage case rate is shown separately.
These are evidence-label metrics, not recall against all potentially relevant
chunks or semantic answer-quality measures.

All30 questions run under every method. Unanswerable/adversarial questions have
no positive labels: exclude them from recall/MRR denominators and report their
hit counts and scope violations separately. Retrieval of a plausible chunk is
not a refusal failure; no answer is generated in this evaluation. Only the24
answerable questions contribute to recall/MRR.

Validate every returned hit against the frozen canonical chunk ID, index, text,
page spans, selected source set and unique contiguous rank. Replays must preserve
run/hit IDs, scores and original duration; verify the database is unchanged.
Report mode/category timing distributions as development observations, not
controlled latency benchmarks.

## Corpus, cost and limits

Use the three previously reviewed synthetic procurement documents (60 chunks)
already retained from the earlier quality run. Prior nine vectors belong to a
different document/index and remain intact; matching page text does not allow
cross-index provenance to be silently reused. New source-specific vectors use
16 bounded batches;30 fixed query embeddings are shared by vector and hybrid
retrieval. Maximum46 calls, each at most8,000 input tokens, ceilingUSD0.007360
at the verified standardUSD0.02/M rate, within the existingUSD8 shared budget.
Stop on the first uncertain operation, preserve its hold and never retry paid
work automatically. No generation call is authorized by this runner.

The retained corpus has no separate foreign-organization invoice mirror.
Question30 therefore tests its explicit quotation-only scope against the other
retained primary sources; it cannot establish adversarial foreign-candidate
ranking resistance. Report this limitation. Existing PostgreSQL tests with
foreign actors/sources remain separate evidence; the broader live security gate
stays open. Direct prompt-injection text is treated as query data; this run does
not test document-embedded attacks or answer-model obedience.

The source pack is wholly fictional and CC0; do not infer real-world accuracy
or compliance. Preserve the failed4/10 live answer baseline and all paid artifacts.
Any later policy change needs a new version and comparison, not a rewritten run.

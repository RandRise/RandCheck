# Phase 3 — Human review and evaluation

**Status:** Proposed  
**Outcome:** Extraction quality is measurable, reproducible, and improved through reviewer corrections.

## Work

- Store machine output immutably by extraction run.
- Store reviewer corrections with user, timestamp, and reason.
- Highlight missing and low-confidence fields.
- Build a versioned labeled invoice dataset.
- Add an evaluation command that can run without the product UI.
- Calculate exact match, precision, recall, correction rate, latency, failure rate, and cost.
- Break results down by field, layout, scan type, and provider version.
- Publish evaluation methodology with the results.

## Acceptance criteria

- [ ] Machine output is never overwritten by a correction.
- [ ] Review history is reconstructable.
- [ ] Dataset version and sample count appear in every report.
- [ ] Evaluation can be repeated from documented steps.
- [ ] Field-level results identify weak areas rather than hiding them in one average.
- [ ] No synthetic demonstration is described as client performance.

## Portfolio evidence

- Versioned evaluation report
- Confusion/error analysis
- Before-and-after provider or prompt comparison
- Review UI showing correction provenance

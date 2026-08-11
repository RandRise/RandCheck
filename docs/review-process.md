# Phase review process

The documentation is the agreement layer between product planning and implementation. Updating a phase document does not silently authorize coding.

## Review cycle

1. **Propose:** document the outcome, scope, exclusions, acceptance criteria, risks, and evidence.
2. **Discuss:** record material choices in the decision log.
3. **Approve:** change the phase status to **Approved** once the open questions are resolved.
4. **Implement:** change the status to **In progress** and work only within the approved boundary.
5. **Verify:** test every acceptance criterion and link the resulting evidence.
6. **Complete:** mark the phase **Complete** only when required evidence exists.

## Change record template

Add this section to a phase when its approved scope changes:

```md
## Change record

### YYYY-MM-DD — Short change title

- Previous decision:
- New decision:
- Reason:
- Effect on scope:
- Effect on later phases:
- Decision-log reference:
```

## What Rand should review

For each phase, focus on four questions:

1. Does the outcome solve a real user problem?
2. Is anything unnecessary included?
3. Can the acceptance criteria be tested objectively?
4. Will the phase create credible portfolio evidence?

## Git review

Because every planning page is Markdown, changes can be reviewed as ordinary Git diffs. The VitePress site is a reading interface; the Markdown files remain the source of truth.

## Status rule

Only Rand approves a phase. An implementation may exist experimentally, but the roadmap must not label the phase complete until its documented acceptance criteria are met.

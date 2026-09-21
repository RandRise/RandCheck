# Local publication review

The user authorized a GitHub checkpoint save on September 21. Prefer a private
repository for this backup; public-release clearance remains incomplete and the
project license is undecided. This checklist does not itself grant publication
permission. The user's explicit save request supersedes the earlier commit/push
deferral without turning the checkpoint into a finished release.

## File selection

Run `.venv/Scripts/python.exe tools/publication_inventory.py --output outputs/publication-inventory.json`
to inventory current Git candidates, including uncommitted source. It records paths,
byte counts and SHA-256 values, rejects redirected files and flags accidental raw
logs, databases, keys, build directories and working outputs. This inventory is not
a secret/PII classifier or a legal clearance. Regenerate it after delivery changes.

Keep source, tests, locked package manifests, workflow, guides, curated evidence,
reviewed sample inputs and their provenance. Generated `output/` and working
`outputs/` directories stay local. No retained file is deleted by these exclusions.
`.env.example` contains public development placeholders; actual environment files,
uploaded documents, keys and databases stay excluded. Inspect the eventual staged
file list separately before any authorized source delivery.

## Sample provenance

| Included material | Evidence / restriction |
| --- | --- |
| Phase 2 synthetic PDFs/images | `samples/phase-2/source-license-register.json`; CC0 fictional content, plus the included Bitstream Vera redistribution notice for embedded fonts |
| Phase 3 smoke labels | `samples/phase-3/smoke-v0.1.0/source-register.json`; references the Phase 2 fixtures |
| Phase 4 procurement inputs | `samples/phase-4/procurement-v0.1.0/README.md`, generation hashes and `tools/phase4_procurement_source_v0.1.0.json`; CC0 fictional content |
| Phase 5 question/due-date inputs | Their sample READMEs, pinned manifests and `tools/phase3_uae_invoice_source_v0.2.0.json`; fictional CC0 source content |
| Screenshots | Curated synthetic application state; review visible text and metadata before publication |
| Dependencies and container images | Their own licenses apply; no blanket project-license claim covers these |

These references establish the recorded provenance; preserve the applicable notices.
The September 17 local review matched 28 registered hash references covering 27
distinct synthetic sample artifacts. All five Phase 8 screenshots were visually checked
against synthetic application state and contained no EXIF fields. Their local hash
and metadata receipt is `outputs/p8-binary-review.json`. This bounded review does not
establish repository-wide absence of private information.

The source repository has no selected project license. Do not infer one from CC0
sample declarations. A dependency license/notice inventory is still required before
calling repository-wide license review complete.

A September 17 lockfile metadata inventory found license declarations for all 531
browser and 180 documentation package entries. Some browser entries declare LGPL,
MPL or CC-BY terms; preserve package-specific notices in any later distribution.
This is metadata coverage, not a compliance decision. The local inventory is
`outputs/p8-npm-license-inventory.json`; .NET/Python/OS notices remain to be reviewed.

## Security and claim checks

Use `tools/phase6_secret_review.py` with a freshly built Locked browser bundle, exact
review decisions and selected log roots. Its reports contain paths/rules/hashes, not
secret values. Previously accepted findings apply only to unchanged file hashes and
matching line/rule locations. Review any new findings individually. The scanner
does not establish absence of private documents or PII; binary evidence needs its
own provenance/visual review.

The UI now describes English review and identifies Arabic/bilingual support as
planned. Historical screenshots retain the wording present when captured and must
be read with their English-only evidence disclaimer. Hosted CI, fresh-checkout
acceptance and production deployment remain unverified. The local demo's fixed
identity, development scanner and unavailable protected-source download remain
explicit limitations.

Before publication: choose the project license, finish dependency notices and binary
review, regenerate the inventory, inspect staged files, and obtain separate approval
for source delivery. No external account is needed for this local preparation.

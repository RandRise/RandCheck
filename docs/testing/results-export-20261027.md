# Saved-result report and CSV verification

October 27, 2026. S5 implements downloads within **See results**. This is
agent-executed local verification, not user acceptance or production sign-in.

## Behavior and boundary

Choose **Readable report (HTML)** or **Spreadsheet (CSV)**, then **Download
results**. The HTML file opens without scripts, remote fonts or network resources
and can be printed by the browser. It is not a server-generated PDF. Names include
the case reference, comparison number and short snapshot identifier.

The new authorized report endpoint requires the displayed case version. A separate
repository reads the selected run, its frozen sources/checks, all decisions and
case version in one PostgreSQL Repeatable Read snapshot. A concurrent decision
cannot create a mixed report. Both file formats use the first captured DTO;
each download rechecks authorization and that version. A changed version requires
refresh rather than an automatic retry with newer decisions. Account, run and
case-version changes clear the client capture and abort pending downloads.

The snapshot ID, case/decision version and saved decision sequences define the
cutoff. The capture time is informational, not a timestamp-only consistency claim.
Case name/reference are metadata at capture; document/review/extraction IDs,
values, checks and evidence belong to the original selected comparison.

Amounts cross the API as invariant decimal strings. HTML escapes all saved text.
CSV is UTF-8 with BOM, quotes multiline cells and protects formula starts,
including leading whitespace/control characters. It has 19 labelled columns.
Protective apostrophes are intentional; HTML retains exact captured text.

## Review findings and fixes

The independent reviewer found reversed Invoice/PO export columns, omitted field
review states, off-by-one item numbers, legacy quotation/evidence mislabelling,
and weak links between repeated-item findings and decisions. All five are fixed
and pass source recheck. The real engine contract is PO-left/Invoice-right; the
signed difference remains invoice minus PO. A real serialized synthetic-policy
fixture now tests that contract, instead of relying only on hand-built examples.

Confirmed/corrected/added fields show effective reviewed values. Unchecked,
cleared and unknown-state fields keep their captured text as explicitly labelled
history, with no confirmed effective value. Item rows remain one-based; header
fields have no item number. Legacy quotation roles and evidence remain intact.
Check/finding identifiers and item identity connect decisions to their checks.

## Fresh evidence

- 46 distinct web tests across latest receipts: 27 export tests plus 19 related
  result/workspace checks. TypeScript and scoped ESLint pass.
- 10 API/security checks, including exact decimal strings, required/stale
  versions, case/run/organization denial, revocation and route classification.
  The three report contract tests were rerun after correcting test-side fixtures.
- Two PostgreSQL tests on the exact disposable `document_intelligence_test`
  database: a decision commits between split reads without entering the old
  snapshot; old-run fields/sources/checks survive re-pinning and foreign scope.
- Four final real browser checks at ports 3310/8310: actual downloads, identical
  report/CSV capture, correct Invoice/PO values/evidence, reopened script-free HTML
  and narrow layout. Zero API writes; retained case JSON unchanged before/after.
- Final downloaded CSV reopened with Python's CSV reader: 101 rows, 19 columns,
  correct role values/evidence, actual decision sequences, version and review states.

Evidence: `outputs/local-completion-20261027/export/final/`. Earlier failed
downloads remain listed in `superseded-downloads.json`; they are not accepted
artifacts. The earlier 16-column receipt is superseded by `final/csv-reopen.json`.
The first build hit DLL locks from the running API; the isolated build passed.
Only the verified owned API process was restarted, against the same database,
Staff identity and disabled workers. No retained/preview reset occurred.

The final layout recheck uses readable minimum-width tables, intact amounts and keyboard-accessible horizontal scrolling at phone width. Independent final evidence review cleared S5 with no remaining material findings. The browser used existing
synthetic reviewed documents through the real app/backend/database. Fresh
upload/worker extraction and scanned Arabic remain S6/L1; this batch does not
close them. Live Entra and deployment remain optional, unverified external tracks.

## Short optional manual checks

1. Open a saved comparison in **See results**. Download HTML, then CSV. Both names
   should contain the same comparison number and snapshot suffix.
2. Open both files. Check the invoice/PO amounts, missing checks, evidence and
   decision history. Unchecked or cleared fields must not appear as confirmed.
3. Record a new decision, refresh results and download again. The new file should
   identify a new captured version; the old downloaded report stays unchanged.

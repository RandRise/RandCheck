# VS Code diagnostic investigation

October 12, 2026. Rand reported about 459 Problems and suspected paths. The later
instruction to leave the document list unchanged supersedes the earlier request
to clear it. No reset, document deletion, preview restart or data write was made.

## Findings and change

The saved VS Code editor state contains two open historical copies:

- `outputs/quotation-workflow-20261012/before/apps/web/app/invoices/components/invoice-document-card.tsx`
- `outputs/guided-review-20261012/before/apps/web/app/page.tsx`

These are the exact before files used for code diffs. They are outside the actual
web project and cannot resolve its relative imports or React dependencies. Using
VS Code's installed TypeScript 6.0.3, a separate reproduction of those files
produced **457 diagnostics**, including missing modules and JSX types. This is
strong evidence for the reported count, not a direct capture of all 459 entries
in the live Problems panel.

A fresh check of the actual web project using its installed TypeScript 5.5.4 passed
with **exit 0 and no diagnostics**. VS Code's TypeScript log showed it was using
its bundled 6.0.3. Its .NET log reported discovering 350 projects and skipping
automatic workspace loading. Its Python log reported scanning 2,285 files and an
enumeration timeout. Historical/generated directories contribute to that noise.

New workspace settings narrowly associate `outputs/**/before/**` with plain text,
preserving the original files and readable diffs while preventing those reference
copies from becoming standalone code projects. Actual source files and tests keep
their normal language services. The settings also identify the web TypeScript SDK,
the real .NET solution and local Python interpreter, and exclude generated/runtime
directories from Python's initial analysis scan. These are editor settings, not
compiler exclusions or relaxed application checks.

## Evidence and remaining limits

Evidence and the captured source baseline are in
`outputs/editor-diagnostics-20261012/`. The command was:

```powershell
.node/node.exe apps/web/node_modules/typescript/bin/tsc --project apps/web/tsconfig.json --noEmit --incremental false
```

No broad rebuild or application test rerun is needed for this editor-only change.
The exact live Problems count after refresh is not independently verified. If the
editor prompts for TypeScript, select **Use Workspace Version**. If old snapshot
diagnostics remain, close and reopen those diff tabs, or use **Developer: Reload
Window** in VS Code. Neither action clears documents in the running application.
Any remaining entry should be inspected by its actual file path and message;
do not assume every residual problem is harmless.

# Third-party materials and notices

October 27, 2026. The application is intentionally **unlicensed** until its owner
chooses otherwise. No permission to reuse the application should be inferred from
the licenses of its dependencies or fictional samples. The owner authorized
GitHub upload at closure; verify the remote receipt separately from this notice.

## What is included

Source dependencies are installed by the package managers; their own copyright,
license and notice files remain applicable. Keep those files when distributing
packages, browser bundles or container images. This page identifies provenance
and metadata; it does not replace the individual licenses or establish compliance
for a future distribution.

| Material | Where to find its terms |
| --- | --- |
| Browser and guide packages | `apps/web/package-lock.json`, `docs/package-lock.json` and each installed package's LICENSE/NOTICE |
| .NET packages | Project package references, restored NuGet `.nuspec` metadata and each package's license/notice files |
| Python packages | `pyproject.toml`, installed distribution metadata and license files |
| Phase 2 fictional documents | `samples/phase-2/source-license-register.json`; CC0 content and the included Bitstream Vera font notice |
| Phase 3–5 fictional inputs | Their source registers, README files and pinned manifests; preserve referenced source and font notices |
| Phase 7 language images | `samples/phase-7/language-v0.1.0/` source records; limited synthetic language fixtures, not customer documents |
| Supplier invoice samples | `samples/supplier-invoice/v0.1.0/README.md`; CC0 fictional content |
| Native table geometry PDF | `samples/supplier-invoice/native-table-v0.1.0/README.md`; CC0 fictional content, core-font references with no embedded font program |
| Application screenshots | Dated synthetic/local or explicitly labelled preview evidence; application licensing remains separate |
| Runtimes, OCR data and OS images | The upstream distribution's licenses and notices; not covered by the application or sample terms |

## Recorded metadata review

`tools/dependency_metadata.py` reads metadata without installing or approving
packages. Use `--group npm`, `nuget` or `python`, `--root` for a source/restore tree,
and a new `--output` path. Python results describe the interpreter running it.
NuGet results describe available restore assets; stale host assets must not be
called the current resolved dependency graph.

The final October 27 lockfile inventory contains 714 npm entries (533 browser and 181
guide), all with declared licenses. Terms include MIT, Apache, BSD, ISC, LGPL,
MPL, CC-BY and others; the presence of metadata does not remove their obligations.
The fresh CI backend image has 102 restored NuGet identities, all with a license
expression, file reference or URL. The host Python inventory has 46 entries,
including two editable records for this unlicensed application; all 44 third-party
entries provide license metadata. It describes Python 3.11.9, not the CI image.
The updated Python 3.11.16 CI image records 67 packages including the unlicensed
application; all 66 third-party entries provide metadata. Its Pillow version is
13.0.0. Exact final records are `dependency-npm-final.json` and
`dependency-python-ci-final.json` alongside the earlier receipts.

Local receipts are under `outputs/local-completion-20261027/l3/`:
`dependency-metadata.json` and `dependency-nuget-ci.json`. Generated receipts and
installed packages stay outside the source delivery. A future binary/container
release needs its own exact resolved inventory, preserved full notices and OS
review. Public release also requires the owner's separate authorization.

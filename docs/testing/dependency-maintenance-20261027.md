# Dependency maintenance

October 27, 2026. The fresh L2 audit found package issues after functional tests
passed. These findings are a release-quality follow-up, separate from P1's Python
file organization. The original failing audit receipts remain in
`outputs/local-completion-20261027/l2/audits/`.

## Findings and changes

- The .NET transitive audit and web application audit passed. The documentation
  graph reported Vite, Vue server-renderer and source-map-js issues.
- Keep the existing Vite 6/VitePress 1 architecture. Update Vite from 6.4.3 to
  6.4.5, Vue's related packages from 3.5.41 to 3.5.44 and source-map-js from 1.2.1
  to 1.2.2. Pin VitePress to the already-used 1.6.4 instead of `latest`; its
  existing Vite override now starts at 6.4.5. The regenerated lock also updates
  related compatible Babel/compiler dependencies. No web-app package changes.
- The Python production audit reported 38 advisories for Pillow 12.3.0. Pin
  Pillow 13.0.0 and update the local extractor's strict runtime guard. A regression
  now checks that the older Pillow version is rejected before document decoding.
  Python 3.11 remains supported. Historical extraction results are unchanged.

Upstream references: [Vite 6.4.5 changes](https://github.com/vitejs/vite/blob/v6.4.5/packages/vite/CHANGELOG.md),
[Vue advisory](https://github.com/vuejs/core/security/advisories/GHSA-g2v6-rqmx-r4w6),
and [Pillow 13 release notes](https://pillow.readthedocs.io/en/stable/releasenotes/13.0.0.html).
Audit results describe the current package advisory databases, not a proof that
all vulnerabilities are absent.

## Verification boundaries

Updated documentation and Python production audits report zero known
vulnerabilities. Python CI `77e67e82e923`, source `8a6f2dd6979a`, passed 338
application, one native, five OCR and 153 pytest-native cases with zero skips,
plus tooling checks; cleanup passed. Receipts remain under
`outputs/local-completion-20261027/dependencies/` and that source copy's CI output.
The owner closed the milestone and requested upload before the additional
packaged-runtime Arabic repetition and final guide build/reading check. Those
remain follow-ups. The older Arabic evidence does not verify Pillow 13.

The old fixture generator remains in its separate pinned environment, with
Pillow 9.4.0 and its immutable generation contract. It runs only on fictional
controlled inputs and is not the upload-processing service. Its seven fixture
checks passed separately. Do not apply the production-runtime audit claim to that
historical generator, the operating system or old retained images.

Existing host environments need the updated project dependencies installed before
starting the new extractor. Do not relax its runtime guard to accept older Pillow.
The existing UI workflow, permissions, stored reviews and comparison outcomes do
not change in this batch.

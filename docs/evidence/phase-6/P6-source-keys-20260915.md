# Protected source-key verification

September 15, 2026. The [contract](../../design/phase-6-source-key-protection.md)
defines explicit certificate-encrypted storage for source-download grants. No browser
screen changed; this increment changes backend configuration and grant protection.

283 distinct tests pass: 253 API and 30 PostgreSQL, with no skips. The full API run
passed 252 cases; the final 23-case key-boundary run replaces matching cases and
adds one malformed-binding check. Documentation and whitespace checks pass.

## Verification snapshot

Final outcomes are recorded in `outputs/phase-6/source-keys-20260915/verification.json`.

| Boundary | Evidence |
| --- | --- |
| Key encryption | Framework XML contains an encrypted secret and no plaintext master key |
| Persistence and isolation | Grants decrypt after provider restart and in another API host with the same application configuration; another application name is refused |
| Certificate rotation | New keys use the replacement certificate; previous certificates preserve existing grants; missing historical decryption material refuses use |
| Invalid resources | Expired/public-only/missing/duplicate certificates, wrong passwords, lost directories, plaintext/malformed/oversized XML and DTDs refuse protection without private diagnostics |
| Invalid configuration | Unsafe paths, content overlap, certificate in the shared ring, invalid names/lifetimes and malformed binding refuse use |
| HTTP authorization | Production requires protected key configuration; shared grants retain current membership checks and durable download authorization audit |
| Read-only preflight | Direct and CLI probes generate no key; full-row database fingerprints and original source bytes remain identical |
| Deployment status | Repository candidate: 11 blocked checks, four passes, seven live requirements; fictional configured candidate still requires verification |

## Artifacts and interpretation

`api-final.trx` covers the complete API regression. `keys-boundary-final.trx`
supersedes matching source-key cases and adds malformed-configuration handling.
`persistence-final.trx` covers source-access and readiness PostgreSQL regression.
`verification.json` and `unique-tests.txt` combine final outcomes by stable test ID.
`repository-report.json` and `fictional-configured-report.json` are sanitized CLI
snapshots. `docs-build.txt` records the documentation build.

Early diagnostic runs exposed a Windows restricted-token PFX-import failure and a
validator mismatch with the framework's XML namespace. The final tests use disposable
self-signed certificates outside that sandbox restriction and the correct namespace.
Earlier failing artifacts remain diagnostic history; they are excluded from acceptance
counts. Temporary exception diagnostics were removed from the application.

The complete API suite and targeted PostgreSQL tests use local fixtures. PostgreSQL
recreates only `document_intelligence_test` on loopback 25434, with one runner. No real
tenant or deployment certificate was used. NuGet audit was disabled for these offline
commands, so these runs do not establish dependency freshness. All 34 retained-table
fingerprints match before/after. No migration or deployed setting changed.

## Remaining evidence

Production source links now require private key configuration. Actual runtime identity
permissions, shared mount behavior, certificate delivery/rotation, protected backups,
Entra/TLS, scanner and private-service checks remain unverified. Cached framework key
revocation is not claimed to be immediate across processes. The application does not
install certificates, set ACLs, encrypt source volumes or activate enterprise deployment.
Phase 6 acceptance remains open.

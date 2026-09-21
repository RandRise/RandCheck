# Frozen synthetic embedding pilot v0.1.0

The subsequent provider-free `--retrieve-saved-plan` runner mode saves/replays
lexical, vector and hybrid runs for the existing query. Its Python-generated
20-candidate lexical plan (`lexical-plan.json`) is pinned to SHA-256
`fbebc28ccc7f5fe1eca82c37f2e817381e7ede6a77ead7d0cec47b0aa6330f07`.
It uses offline clients and the already paid query; no key or worker is required.
This does not modify the frozen paid operations below.

Frozen before any dispatch on 2026-09-10. `plan.json` SHA-256:
`ba9f727dccf93833ae08e630af5aa4f851e212928bb6f0b265a159a5aaa90099`.

The plan embeds the nine canonical chunks (1,153 UTF-8 text bytes) of the existing
synthetic `fta-standard-services-native.pdf` invoice, in batches of four, four,
and one. A fourth operation embeds the fixed question, "What amount is due on the
invoice?", and searches only that index. Operation IDs, source hashes and model
identity are fixed. This is a storage/search smoke test, not an accuracy benchmark.

Model: `text-embedding-3-small`, 1,536 dimensions, float output, cosine distance.
The official [model pricing](https://developers.openai.com/api/docs/models/text-embedding-3-small)
was checked on 2026-09-10: standard input USD 0.02 per million tokens. New admission
uses a rate window ending 2026-09-11 00:00 UTC. Each operation allows at most 8,000
input tokens: four reservations can total at most USD 0.000640, within the existing
shared USD 8 ceiling. The ledger rounds each charge upward to six decimal places;
its conservative total can exceed the exact token-rate calculation.

Rand already authorized paid pilot work and persistent invoice embeddings.
`tools/phase5_run_embedding_pilot.ps1 -ExecutePaid` verifies this fixed source plan
before reading the local key. It launches a private loopback service, sends the key
only to that service, executes each operation once, and stops both processes.
It refuses to overwrite a prior attempt report. Inspect the saved ledger after
any failure; never repeat uncertain paid work or release an unknown hold.

Runner modes: `--verify-plan` performs source checks without a key or provider;
`--execute-plan` uses the private worker; `--replay-plan` injects an offline client
that throws if preparation or dispatch is attempted. Replays reuse settled raw
outcomes and stored vectors. No public endpoint or answer-generation gate is enabled.

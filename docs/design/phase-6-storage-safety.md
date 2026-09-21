# Phase 6 storage admission and malware screening

Implemented September 12, 2026. This increment covers local source admission and
screening before extraction. It adds no database migration and does not modify
retained Phase 5 files, documents or spending.

## Admission

`Storage:SecurityMode` resolves to Development only in Development/Testing.
Other environments default to Locked; explicitly requesting Development there
also resolves to Locked. Enterprise uploads require Enforced mode, an explicit
quarantine directory and a configured scanner. Missing or invalid configuration
fails closed. The API accepts at most 10 MiB per multipart section; the storage
copy also enforces the declared size and configured file limit before scanning.

Sources use organization/document GUID paths and a bounded alphanumeric extension.
The original filename is never a storage path. Storage rejects traversal, alternate
data stream syntax and existing reparse points in the path or root ancestors.
Enforced quarantine must be a separate sibling of the upload root. Files receive
random staging names and remain open exclusively while copying, hashing and scanning.
The input copy reads at most one byte beyond its declared size before rejection.

A clean result and unchanged SHA-256 permit promotion with overwrite disabled.
A competing destination is preserved. Rejected, interrupted and unavailable scans
remove their owned staging file. Empty directories may remain. Process termination
can leave staging files; automatic quarantine retention/cleanup is not implemented.
Quarantine is temporary admission storage, not a retained malware evidence archive.
The subsequent [retention increment](phase-6-retention-controls.md) adds leased,
organization-bound staging and explicit audited cleanup of abandoned uploads;
legacy staging names remain preserved.

Successful admission supplies an in-process receipt binding hash, bytes and scanner
policy to the document. The enforced upload queue requires a receipt and matching
bound organization/actor audit context. The clean-scan event, document, run and job
commit together. If that transaction fails, upload compensation removes the promoted
source. No scan receipt is accepted from the HTTP request. Rejected files return 422;
scanner unavailability returns 503. Independent request-failure audit
uses the existing safe HTTP classification without original filename or daemon text.

## Scanner transport and deployment

`MalwareScan:Mode=ClamAv` uses the clamd INSTREAM protocol with literal loopback
addresses only. The client sends file bytes, never a filesystem path. It bounds
file bytes, reply bytes and duration; requires exactly one complete clean response
and EOF; and treats detection, malformed/truncated replies, errors, disconnects
and timeouts as blocked admission. Caller cancellation remains cancellation.
This follows the [official clamd protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html).

Example application settings (directories must be private sibling paths):

```json
{
  "Storage": {
    "SecurityMode": "Enforced",
    "UploadDirectory": "/private/documents/uploads",
    "QuarantineDirectory": "/private/documents/quarantine",
    "MaxUploadBytes": 10485760
  },
  "MalwareScan": {
    "Mode": "ClamAv",
    "Address": "127.0.0.1",
    "Port": 3310,
    "TimeoutSeconds": 30
  }
}
```

The clamd TCP interface has no transport authentication or encryption. Deployment
must isolate it locally and manage daemon configuration, signatures and updates.
ClamAV limits can skip content; daemon policies must reject uninspectable content,
including encrypted archives and exceeded scan limits, before production use.
See [ClamAV scanning guidance](https://docs.clamav.net/manual/Usage/Scanning.html).
The application does not attest daemon configuration or definition freshness.
Transport fixtures and injected outcomes do not establish detection quality.
No real ClamAV daemon or signature database was verified in this increment.

## Worker screening and audit

Enforced workers verify the claimed organization/document source path, exact bytes
and stored SHA-256, scan the current source, and recheck the hash before invoking
the extractor. Existing files are preserved on rejection. Python's independent
stored-hash check remains in place. Unavailable scanning uses the existing bounded
retry policy (maximum three attempts); detection and integrity failures are terminal.
Worker transition audits use fixed reason codes `malware_scan_unavailable`,
`malware_detected` and `storage_integrity_failed` under the provisioned System actor.
Lease checks, queue updates and transition audit remain transactional.

## Remaining controls

Deployment must enforce private filesystem ACLs, encryption and backups. Reparse
checks do not replace OS permissions against a concurrent privileged local attacker.
The scanner, API and worker host are trusted components. The subsequent
[source-access increment](phase-6-source-access.md) adds authenticated five-minute
original-file grants. Retention policy previews, holds and explicit quarantine
cleanup now have a [separate contract](phase-6-retention-controls.md). Stored-document
deletion and backup lifecycle remain Phase 6 work. Live Entra/TLS checks,
full API runtime grants and whole-phase security acceptance also remain open.

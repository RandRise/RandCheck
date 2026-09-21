# Protected source-grant keys

September 15, 2026. Original-file grants now use an explicitly configured, isolated
ASP.NET Data Protection provider. Certificate mode persists encrypted keys in a
shared directory and binds grants to an application name. Membership, tenant,
document, integrity, scanning, five-minute expiry and audit checks still apply on
every download. This does not encrypt stored source files.

## Configuration

Supply these values through private backend configuration. No production certificate
or password belongs in source control, browser settings or readiness output.

| `SourceKeyProtection` field | Requirement |
| --- | --- |
| `Mode` | `Certificate` for enterprise source downloads |
| `ApplicationName` | Stable name shared by replicas of one deployment; 3–100 letters, digits, dots, underscores or hyphens, beginning with a letter or digit |
| `KeyDirectory` | Absolute path to an existing dedicated shared directory |
| `CertificatePath` | Absolute path to the current private PFX, outside the shared key directory and content/quarantine/deletion storage |
| `CertificatePassword` | PFX password from private secret delivery; empty is supported if the PFX itself has no password |
| `PreviousCertificates` | Optional array of `{ Path, Password }` for decryption during rotation; maximum eight |
| `KeyLifetimeDays` | 7–365; default 90 |

The wrapping certificate must have an RSA private key of at least 2048 bits and be
currently valid. Previous certificates may be expired, because they only decrypt
older keys. Duplicate certificates are refused. PFX imports use ephemeral key storage;
the application does not install certificates into the OS certificate store.

Development and Testing retain the default provider when mode is absent or
`Development`. Other environments refuse those defaults. Unknown modes lock source
grants. API startup remains available; issuance returns a generic 503 if configured
keys are unavailable, and an unreadable grant cannot release a file.

## Storage and failure behavior

The operator creates the key directory and supplies certificates before activation.
The application never creates a missing directory. An existing empty directory may
receive its first encrypted key when a grant is issued. Key storage must not overlap
source, quarantine or deletion-journal roots. Files and ancestors must not be reparse
points. These checks do not establish ACLs or prevent a privileged filesystem race.

The repository permits at most 100 entries, including framework temporary files,
and at most 64 KiB per XML file. It rejects plaintext master keys, unrecognized
encryption envelopes, malformed XML and DTDs. Existing key descriptors must decrypt
before the provider can be used; missing previous certificates do not trigger silent
replacement of unreadable history. Repository and certificate errors are sanitized,
and the isolated provider has no logging sinks for framework path diagnostics.

Every protection operation checks directory contents and current certificate expiry,
including after the framework caches a key. A missing mount fails closed. This is
not continuous verification of the certificate files: the provider retains loaded
certificates until restart. It also cannot distinguish an intentionally new empty
directory from an incorrectly mounted empty directory. Monitor the intended mount
and preserve the ring independently.

## Rotation and recovery

Use ASP.NET's [certificate protection and decryption rotation support](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-8.0).
All replicas must receive the same current and required previous certificates,
shared directory and application name. Restart after configuration changes. New keys
use the current certificate; existing XML is not rewrapped automatically. Keep old
certificates while any retained key needs them. The provider refuses an unreadable
historical key even after the corresponding five-minute grants expire.

Key generation and revocation use framework semantics and caching. Revocation is not
an immediate cross-process kill switch; restart replicas and verify invalidation as
part of a compromise procedure. Membership revocation remains independently checked
from the database on requests. Changing the application name or losing required keys
invalidates outstanding grants. Restore the intended encrypted ring together with
its wrapping certificates; do not delete history to clear an error.

Only deployment identities should access these resources. Key encryption does not
protect against compromise of an API identity that can read both ring and private
certificate, or against an operator who can replace trusted key descriptors. Maintain
private certificate delivery, shared-directory ACLs, protected backups, expiry alerts
and operator change records. Key operations do not insert business audit events;
source grant issuance/download authorization retain their existing durable audit.

## Verification and activation

Preflight adds `source-keys.profile` for configuration and `observed.source-keys`
for existing directories/certificates and descriptor decryption. The latter generates
no key or grant, so an empty directory does not prove write permissions. `live.keys`
still requires actual deployment identity/ACL, mount, rotation, restart and replica
evidence. A local test is insufficient to mark deployment verified.

See the [verification report](../evidence/phase-6/P6-source-keys-20260915.md).
Live Entra/TLS, scanner, private-service rotation, storage permissions, recovery
freshness and patched runtime checks remain prerequisites for enterprise activation.

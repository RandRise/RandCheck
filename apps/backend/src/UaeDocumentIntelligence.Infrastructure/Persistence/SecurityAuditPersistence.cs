using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed partial class AppDbContext
{
    private IReadOnlyList<SecurityAuditEvent> PrepareSecurityAudit()
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        foreach (var e in changes)
        {
            if (e.Entity is OrganizationCapacityPolicy && (e.State == EntityState.Deleted ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name == nameof(OrganizationCapacityPolicy.OrganizationId))))
                throw new InvalidOperationException("Capacity policy identity cannot be removed or remapped.");
            if (e.Entity is DocumentDeletion && e.State != EntityState.Added &&
                (e.State == EntityState.Deleted || e.Property(nameof(DocumentDeletion.CompletedAtUtc)).OriginalValue is not null ||
                 e.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(DocumentDeletion.CompletedAtUtc))))
                throw new InvalidOperationException("Deletion identity and completion history are immutable.");
            if (e.Entity is RetentionHold && e.State != EntityState.Added &&
                (e.State == EntityState.Deleted || e.Property(nameof(RetentionHold.ReleasedAtUtc)).OriginalValue is not null ||
                 e.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(RetentionHold.ReleasedAtUtc) or nameof(RetentionHold.Version)))))
                throw new InvalidOperationException("Hold history cannot be replaced or deleted.");
            if (e.Entity is OrganizationRetentionPolicy && e.State == EntityState.Deleted)
                throw new InvalidOperationException("Retention policies cannot be deleted.");
            if (e.Entity is SecurityAuditEvent or EnterpriseIdentity && e.State != EntityState.Added)
                throw new InvalidOperationException("Security audit and enterprise identities are immutable.");
            if (e.Entity is OrganizationMembership && (e.State == EntityState.Deleted ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not
                    (nameof(OrganizationMembership.Role) or
                     nameof(OrganizationMembership.IsActive) or nameof(OrganizationMembership.Version) or nameof(OrganizationMembership.UpdatedAtUtc)))))
                throw new InvalidOperationException("Membership identity and history cannot be replaced or deleted.");
        }
        if (audit?.IsBound != true) return [];
        var result = new List<SecurityAuditEvent>();
        foreach (var e in changes)
        {
            var descriptor = e.Entity switch
            {
                Organization x => (x.Id, x.Id, "organization", "organization.created", (string?)null),
                DocumentDeletion x => (x.OrganizationId, x.DocumentId, "document", e.State == EntityState.Added ? "document.database-content-removed" : "document.deletion-completed", (string?)null),
                OrganizationRetentionPolicy x => (x.OrganizationId, x.OrganizationId, "retention-policy", "retention.policy-changed", "retention-policy"),
                RetentionHold x => (x.OrganizationId, x.Id, "retention-hold", e.State == EntityState.Added ? "retention.hold-placed" : "retention.hold-released", "retention-hold"),
                OrganizationMembership x => (x.OrganizationId, x.Id, "membership", e.State == EntityState.Added ? "membership.granted" : "membership.changed", "membership"),
                Document x => (x.OrganizationId, x.Id, "document", e.State == EntityState.Added ? "document.uploaded" : "document.changed", "ProcessingStatus"),
                ExtractionJob x => (x.OrganizationId, x.Id, "extraction-job", e.State == EntityState.Added ? "processing.queued" : "processing.changed", "Status"),
                ReviewSession x => (x.OrganizationId, x.Id, "review", e.State == EntityState.Added ? "review.started" : "review.changed", "Status"),
                ReviewAction x => (x.OrganizationId, x.Id, "review-action", "review.action-recorded", "Action"),
                ReconciliationCase x when e.State == EntityState.Modified &&
                    (e.Property(nameof(ReconciliationCase.DraftInvoiceDocumentId)).IsModified || e.Property(nameof(ReconciliationCase.DraftPurchaseOrderDocumentId)).IsModified)
                    => (x.OrganizationId, x.Id, "reconciliation-case", "reconciliation.draft-changed", "case-drafts"),
                ReconciliationDraftRequest x => (x.OrganizationId, x.ReconciliationCaseId, "reconciliation-case", "reconciliation.draft-intent-recorded", (string?)null),
                ReconciliationCase x when e.State == EntityState.Modified &&
                    (e.Property(nameof(ReconciliationCase.Name)).IsModified || e.Property(nameof(ReconciliationCase.Category)).IsModified)
                    => (x.OrganizationId, x.Id, "reconciliation-case", "reconciliation.metadata-changed", "case-metadata"),
                ReconciliationCase x => (x.OrganizationId, x.Id, "reconciliation-case", "reconciliation.case-changed", "Status"),
                ReconciliationDocumentLink x => (x.OrganizationId, x.Id, "reconciliation-link", e.State == EntityState.Deleted ? "reconciliation.document-unlinked" : "reconciliation.document-linked", "Role"),
                ReconciliationRun x => (x.OrganizationId, x.Id, "reconciliation-run", "reconciliation.evaluated", "Status"),
                FindingResolution x => (x.OrganizationId, x.Id, "finding-resolution", "reconciliation.finding-resolved", "Action"),
                CitedDocumentIndex x => (x.OrganizationId, x.Id, "cited-index", "evidence.index-changed", "Status"),
                CitedRetrievalRun x => (x.OrganizationId, x.Id, "question", "question.saved", "Status"),
                _ => (Guid.Empty, Guid.Empty, "", "", (string?)null)
            };
            if (descriptor.Item1 == Guid.Empty) continue;
            if (descriptor.Item1 != audit.OrganizationId)
                throw new InvalidOperationException("An audited request cannot write another organization's data.");
            // Replacing a case's live link removes only that association. Historical
            // evaluation snapshots keep their own document/review/run references.
            if (e.State == EntityState.Deleted && e.Entity is not ReconciliationDocumentLink)
                throw new InvalidOperationException("Audited workflow deletion requires a retention policy.");
            var before = e.State == EntityState.Added ? null : SafeState(e, descriptor.Item5, true);
            var after = e.State == EntityState.Deleted ? null : SafeState(e, descriptor.Item5, false);
            result.Add(audit.Event(descriptor.Item4, descriptor.Item3, descriptor.Item2, "Succeeded", "committed", before: before, after: after));
        }
        SecurityAuditEvents.AddRange(result);
        return result;
    }

    private static string? SafeState(EntityEntry entry, string? property, bool original)
    {
        object? Value(string name) => original ? entry.Property(name).OriginalValue : entry.Property(name).CurrentValue;
        if (property == "membership")
            return $"{Value("Role")};active={Value("IsActive")}";
        if (property == "case-metadata")
            return $"status={Value("Status")};category={Value("Category")};titleChanged={entry.Property(nameof(ReconciliationCase.Name)).IsModified}";
        if (property == "case-drafts")
            return $"invoice={Value(nameof(ReconciliationCase.DraftInvoiceDocumentId))};purchaseOrder={Value(nameof(ReconciliationCase.DraftPurchaseOrderDocumentId))}";
        if (property == "retention-policy")
            return $"documentDays={Value("DocumentDays") ?? "indefinite"};quarantineHours={Value("QuarantineHours") ?? "disabled"}";
        if (property == "retention-hold")
            return $"document={Value("DocumentId") ?? "organization"};reason={Value("Reason")};released={Value("ReleasedAtUtc") is not null}";
        if (property is null) return null;
        var value = Value(property);
        // Only enumerated workflow state; never generic object serialization or user text.
        return value is Enum ? value.ToString() : null;
    }
    private void DetachAudit(IReadOnlyList<SecurityAuditEvent> events)
    {
        foreach (var item in events) Entry(item).State = EntityState.Detached;
    }
}

public sealed class SecurityAuditSink(DbContextOptions<AppDbContext> options) : ISecurityAuditSink
{
    public async Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        // Independent context: rejected business changes must never be accidentally saved.
        await using var isolated = new AppDbContext(options);
        isolated.SecurityAuditEvents.Add(auditEvent);
        await isolated.SaveChangesAsync(cancellationToken);
    }
}

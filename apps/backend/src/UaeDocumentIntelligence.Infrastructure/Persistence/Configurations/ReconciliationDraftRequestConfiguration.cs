using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ReconciliationDraftRequestConfiguration : IEntityTypeConfiguration<ReconciliationDraftRequest>
{
    public void Configure(EntityTypeBuilder<ReconciliationDraftRequest> b)
    {
        b.ToTable("reconciliation_draft_requests", t =>
        {
            t.HasCheckConstraint("ck_reconciliation_draft_requests_role", "role IN ('Invoice','PurchaseOrder')");
            t.HasCheckConstraint("ck_reconciliation_draft_requests_key", "length(btrim(request_key)) > 0 AND request_key = btrim(request_key)");
            t.HasCheckConstraint("ck_reconciliation_draft_requests_hash", "payload_hash ~ '^[0-9a-f]{64}$'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.ReconciliationCaseId).HasColumnName("reconciliation_case_id");
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.RequestKey).HasColumnName("request_key").HasMaxLength(100);
        b.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasMaxLength(64);
        b.Property(x => x.ExpectedVersion).HasColumnName("expected_version");
        b.Property(x => x.DraftVersion).HasColumnName("draft_version");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasIndex(x => new { x.OrganizationId, x.ActorId, x.RequestKey }).IsUnique().HasDatabaseName("ux_reconciliation_draft_requests_intent");
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReconciliationCase>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReconciliationCaseId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

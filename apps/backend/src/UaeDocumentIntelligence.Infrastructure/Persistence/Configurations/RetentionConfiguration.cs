using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationRetentionPolicyConfiguration : IEntityTypeConfiguration<OrganizationRetentionPolicy>
{
    public void Configure(EntityTypeBuilder<OrganizationRetentionPolicy> b)
    {
        b.ToTable("organization_retention_policies", t =>
        {
            t.HasCheckConstraint("ck_retention_document_days", "document_days IS NULL OR document_days BETWEEN 1 AND 36500");
            t.HasCheckConstraint("ck_retention_quarantine_hours", "quarantine_hours IS NULL OR quarantine_hours BETWEEN 1 AND 87600");
        });
        b.HasKey(x => x.OrganizationId);
        b.Property(x => x.OrganizationId).HasColumnName("organization_id").ValueGeneratedNever();
        b.Property(x => x.DocumentDays).HasColumnName("document_days");
        b.Property(x => x.QuarantineHours).HasColumnName("quarantine_hours");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RetentionHoldConfiguration : IEntityTypeConfiguration<RetentionHold>
{
    public void Configure(EntityTypeBuilder<RetentionHold> b)
    {
        b.ToTable("retention_holds", t =>
        {
            t.HasCheckConstraint("ck_retention_hold_reason", "reason IN ('Investigation','LegalHold','AdministrativeReview')");
            t.HasCheckConstraint("ck_retention_hold_release", "released_at_utc IS NULL OR released_at_utc >= created_at_utc");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.ReleasedAtUtc).HasColumnName("released_at_utc");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId }).HasFilter("released_at_utc IS NULL");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        // Insert trigger verifies the live document and organization. Released hold
        // history keeps its UUID after the document's authorized purge.
    }
}

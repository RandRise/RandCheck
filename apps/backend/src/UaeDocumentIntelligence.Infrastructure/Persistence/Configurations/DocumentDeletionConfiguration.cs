using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class DocumentDeletionConfiguration : IEntityTypeConfiguration<DocumentDeletion>
{
    public void Configure(EntityTypeBuilder<DocumentDeletion> b)
    {
        b.ToTable("document_deletions", t =>
        {
            t.HasCheckConstraint("ck_document_deletion_size", "size_bytes > 0 AND size_bytes <= 10485760");
            t.HasCheckConstraint("ck_document_deletion_hash", "sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_document_deletion_time", "completed_at_utc IS NULL OR completed_at_utc >= requested_at_utc");
        });
        CitedMapping.Identity(b);
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId }).IsUnique();
        b.Property(x => x.StorageKey).HasMaxLength(255);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.CompletedAtUtc).IsConcurrencyToken();
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        // The document/content rows are removed; this minimum identity survives for audit and restore checks.
    }
}

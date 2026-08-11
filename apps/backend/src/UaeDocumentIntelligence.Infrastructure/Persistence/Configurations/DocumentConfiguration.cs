using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("documents", t =>
        {
            t.HasCheckConstraint("ck_documents_size", "size_bytes > 0");
            t.HasCheckConstraint("ck_documents_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_documents_status", "processing_status IN ('Stored','Processing','ReadyForReview','ExtractionFailed','ProcessingCancelled')");
            t.HasCheckConstraint("ck_documents_timestamps", "updated_at_utc >= created_at_utc");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.CreatedByActorId).HasColumnName("created_by_actor_id");
        b.Property(x => x.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255);
        b.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(255);
        b.Property(x => x.DeclaredContentType).HasColumnName("declared_content_type").HasMaxLength(100);
        b.Property(x => x.DetectedContentType).HasColumnName("detected_content_type").HasMaxLength(100);
        b.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        b.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("character(64)");
        b.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.CreatedByActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.StorageKey).IsUnique().HasDatabaseName("ux_documents_storage_key");
        b.HasIndex(x => new { x.OrganizationId, x.Sha256 }).HasDatabaseName("ix_documents_org_sha256");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc }).HasDatabaseName("ix_documents_org_created");
        b.HasIndex(x => new { x.OrganizationId, x.ProcessingStatus, x.CreatedAtUtc }).HasDatabaseName("ix_documents_org_status_created");
        b.Navigation(x => x.ExtractionRuns).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.ReviewSessions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

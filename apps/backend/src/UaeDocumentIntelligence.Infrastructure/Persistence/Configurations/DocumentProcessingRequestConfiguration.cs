using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class DocumentProcessingRequestConfiguration : IEntityTypeConfiguration<DocumentProcessingRequest>
{
    public void Configure(EntityTypeBuilder<DocumentProcessingRequest> b)
    {
        b.ToTable("document_processing_requests", t =>
        {
            t.HasCheckConstraint("ck_document_processing_requests_kind", "kind IN ('Upload','Reprocess')");
            t.HasCheckConstraint("ck_document_processing_requests_key", "request_key ~ '^[A-Za-z0-9._:-]{1,100}$'");
            t.HasCheckConstraint("ck_document_processing_requests_hash", "payload_hash ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_document_processing_requests_run", "run_number > 0");
        });
        b.HasKey(x => new { x.OrganizationId, x.ActorId, x.Kind, x.Key });
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(16);
        b.Property(x => x.Key).HasColumnName("request_key").HasMaxLength(100);
        b.Property(x => x.PayloadHash).HasColumnName("payload_hash").HasColumnType("character(64)");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.JobId).HasColumnName("job_id");
        b.Property(x => x.RunNumber).HasColumnName("run_number");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        // Source/run/job IDs are tombstone references, deliberately not cascade FKs.
    }
}

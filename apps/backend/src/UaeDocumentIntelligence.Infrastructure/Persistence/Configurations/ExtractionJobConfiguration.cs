using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ExtractionJobConfiguration : IEntityTypeConfiguration<ExtractionJob>
{
    public void Configure(EntityTypeBuilder<ExtractionJob> b)
    {
        b.ToTable("extraction_jobs", t =>
        {
            t.HasCheckConstraint("ck_extraction_jobs_status",
                "status IN ('Pending','Leased','RetryScheduled','Succeeded','Failed','Cancelled')");
            t.HasCheckConstraint("ck_extraction_jobs_attempts", "attempt_count >= 0 AND attempt_count <= 3");
            t.HasCheckConstraint("ck_extraction_jobs_lease_shape",
                "(status = 'Leased' AND lease_token IS NOT NULL AND lease_owner IS NOT NULL AND " +
                "lease_expires_at_utc IS NOT NULL AND last_heartbeat_at_utc IS NOT NULL) OR " +
                "(status <> 'Leased' AND lease_token IS NULL AND lease_owner IS NULL AND " +
                "lease_expires_at_utc IS NULL AND last_heartbeat_at_utc IS NULL)");
            t.HasCheckConstraint("ck_extraction_jobs_completion",
                "(status IN ('Succeeded','Failed','Cancelled') AND completed_at_utc IS NOT NULL) OR " +
                "(status NOT IN ('Succeeded','Failed','Cancelled') AND completed_at_utc IS NULL)");
            t.HasCheckConstraint("ck_extraction_jobs_result",
                "(status = 'Succeeded' AND result_outcome IN ('Ready','Partial')) OR " +
                "(status <> 'Succeeded' AND result_outcome IS NULL)");
            t.HasCheckConstraint("ck_extraction_jobs_timestamps",
                "updated_at_utc >= created_at_utc AND available_at_utc >= created_at_utc");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        b.Property(x => x.AvailableAtUtc).HasColumnName("available_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.LastClaimedAtUtc).HasColumnName("last_claimed_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.LeaseToken).HasColumnName("lease_token").IsConcurrencyToken();
        b.Property(x => x.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(100);
        b.Property(x => x.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.LastHeartbeatAtUtc).HasColumnName("last_heartbeat_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        b.Property(x => x.LastSafeErrorSummary).HasColumnName("last_safe_error_summary").HasMaxLength(500);
        b.Property(x => x.ResultOutcome).HasColumnName("result_outcome").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ExtractionRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ExtractionRunId }).IsUnique()
            .HasDatabaseName("ux_extraction_jobs_org_run");
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId }).IsUnique()
            .HasFilter("status IN ('Pending','Leased','RetryScheduled')")
            .HasDatabaseName("ux_extraction_jobs_org_document_active");
        b.HasIndex(x => new { x.AvailableAtUtc, x.CreatedAtUtc, x.Id })
            .HasFilter("status IN ('Pending','RetryScheduled')")
            .HasDatabaseName("ix_extraction_jobs_due");
        b.HasIndex(x => x.LeaseExpiresAtUtc).HasFilter("status = 'Leased'")
            .HasDatabaseName("ix_extraction_jobs_expired_lease");
    }
}

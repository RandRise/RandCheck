using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ReconciliationCheckConfiguration : IEntityTypeConfiguration<ReconciliationCheck>
{
    public void Configure(EntityTypeBuilder<ReconciliationCheck> b)
    {
        b.ToTable("reconciliation_checks", table =>
        {
            table.HasCheckConstraint("ck_reconciliation_checks_status", "status IN ('NoDifference','DifferenceFound','CouldNotCheck')");
            table.HasCheckConstraint("ck_reconciliation_checks_occurrence", "occurrence_index IS NULL OR occurrence_index >= 0");
            table.HasCheckConstraint("ck_reconciliation_checks_numeric", "(tolerance IS NULL OR tolerance >= 0) AND (status <> 'CouldNotCheck' OR signed_difference IS NULL)");
            table.HasCheckConstraint("ck_reconciliation_checks_finding", "status = 'NoDifference' OR finding_id IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReconciliationRunId).HasColumnName("reconciliation_run_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(50);
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index");
        b.Property(x => x.LineIdentity).HasColumnName("line_identity");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(100);
        b.Property(x => x.Explanation).HasColumnName("explanation").HasMaxLength(1000);
        b.Property(x => x.LeftFieldSnapshotId).HasColumnName("left_field_snapshot_id");
        b.Property(x => x.RightFieldSnapshotId).HasColumnName("right_field_snapshot_id");
        b.Property(x => x.LeftValue).HasColumnName("left_value");
        b.Property(x => x.RightValue).HasColumnName("right_value");
        b.Property(x => x.SignedDifference).HasColumnName("signed_difference").HasPrecision(24, 6);
        b.Property(x => x.Tolerance).HasColumnName("tolerance").HasPrecision(18, 6);
        b.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(32);
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
        b.Property(x => x.Basis).HasColumnName("basis").HasMaxLength(100);
        b.Property(x => x.FindingId).HasColumnName("finding_id");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationRun>().WithMany(x => x.Checks)
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ReconciliationFieldSnapshot>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.LeftFieldSnapshotId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReconciliationFieldSnapshot>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.RightFieldSnapshotId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReconciliationFinding>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.FindingId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id });
    }
}

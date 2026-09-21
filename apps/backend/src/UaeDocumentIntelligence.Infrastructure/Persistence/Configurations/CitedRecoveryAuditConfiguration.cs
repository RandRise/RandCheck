using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class CitedRecoveryAuditConfiguration : IEntityTypeConfiguration<CitedRecoveryAudit>
{
    public void Configure(EntityTypeBuilder<CitedRecoveryAudit> b)
    {
        b.ToTable("cited_recovery_audits", t =>
        {
            t.HasCheckConstraint("ck_cited_recovery_sequence", "before_event_sequence > 0 AND after_event_sequence = before_event_sequence + 1");
            t.HasCheckConstraint("ck_cited_recovery_hashes", "command_sha256 ~ '^[0-9a-f]{64}$' AND (evidence_sha256 IS NULL OR evidence_sha256 ~ '^[0-9a-f]{64}$')");
            t.HasCheckConstraint("ck_cited_recovery_transition", "(action = 'CancelReserved' AND before_state = 'Reserved' AND after_state = 'Cancelled' AND reason_code = 'not_dispatched' AND actual_usd IS NULL AND evidence_id IS NULL AND evidence_reference IS NULL AND evidence_sha256 IS NULL) OR (action = 'RecordVerifiedCost' AND before_state IN ('Dispatched','Uncertain') AND after_state = 'Settled' AND reason_code = 'verified_final_cost' AND actual_usd IS NOT NULL AND actual_usd >= 0 AND evidence_id IS NOT NULL AND evidence_reference IS NOT NULL AND evidence_sha256 IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.RequestId).HasColumnName("request_id");
        b.Property(x => x.ReservationId).HasColumnName("reservation_id");
        b.Property(x => x.OriginalActorId).HasColumnName("original_actor_id");
        b.Property(x => x.OperatorActorId).HasColumnName("operator_actor_id");
        b.Property(x => x.SpendEventId).HasColumnName("spend_event_id");
        b.Property(x => x.PolicyVersion).HasColumnName("policy_version").HasMaxLength(100);
        b.Property(x => x.CommandSha256).HasColumnName("command_sha256").HasMaxLength(64);
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(40);
        b.Property(x => x.BeforeState).HasColumnName("before_state").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.AfterState).HasColumnName("after_state").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.BeforeEventSequence).HasColumnName("before_event_sequence");
        b.Property(x => x.AfterEventSequence).HasColumnName("after_event_sequence");
        b.Property(x => x.ActualUsd).HasColumnName("actual_usd").HasPrecision(18, 6);
        b.Property(x => x.EvidenceId).HasColumnName("evidence_id");
        b.Property(x => x.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(100);
        b.Property(x => x.EvidenceSha256).HasColumnName("evidence_sha256").HasMaxLength(64);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<CitedSpendReservation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReservationId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OriginalActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.OperatorActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CitedSpendEvent>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.SpendEventId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.RequestId }).IsUnique().HasDatabaseName("ux_cited_recovery_request");
        b.HasIndex(x => x.ReservationId).IsUnique().HasDatabaseName("ux_cited_recovery_reservation");
        b.HasIndex(x => x.EvidenceId).IsUnique().HasFilter("evidence_id IS NOT NULL").HasDatabaseName("ux_cited_recovery_evidence");
        b.HasIndex(x => x.EvidenceReference).IsUnique().HasFilter("evidence_reference IS NOT NULL").HasDatabaseName("ux_cited_recovery_reference");
    }
}

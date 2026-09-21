using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class CitedPilotBudgetConfiguration : IEntityTypeConfiguration<CitedPilotBudget>
{
    public void Configure(EntityTypeBuilder<CitedPilotBudget> b)
    {
        b.ToTable("cited_pilot_budgets", t =>
        {
            t.HasCheckConstraint("ck_cited_pilot_singleton", "id = 1");
            t.HasCheckConstraint("ck_cited_pilot_amounts", "ceiling_usd > 0 AND ceiling_usd <= 8 AND settled_usd >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.CeilingUsd).HasColumnName("ceiling_usd").HasPrecision(18, 6);
        b.Property(x => x.SettledUsd).HasColumnName("settled_usd").HasPrecision(18, 6);
        b.Property(x => x.RequiresReconciliation).HasColumnName("requires_reconciliation");
        b.HasData(new CitedPilotBudget());
    }
}

internal sealed class CitedSpendReservationConfiguration : IEntityTypeConfiguration<CitedSpendReservation>
{
    public void Configure(EntityTypeBuilder<CitedSpendReservation> b)
    {
        b.ToTable("cited_spend_reservations", t =>
        {
            t.HasCheckConstraint("ck_cited_spend_amounts", "reserved_usd > 0 AND reserved_usd <= 8 AND (actual_usd IS NULL OR actual_usd >= 0)");
            t.HasCheckConstraint("ck_cited_spend_hashes", "fingerprint ~ '^[0-9a-f]{64}$' AND request_sha256 ~ '^[0-9a-f]{64}$'");
            t.HasCheckConstraint("ck_cited_spend_tokens", "maximum_input_tokens BETWEEN 1 AND 8000 AND ((workload = 'Generation' AND maximum_output_tokens BETWEEN 1 AND 4000) OR (workload = 'Embedding' AND maximum_output_tokens = 0))");
            t.HasCheckConstraint("ck_cited_spend_workload", "workload IN ('Generation','Embedding')");
            t.HasCheckConstraint("ck_cited_spend_state", "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled','HeldUnknown')");
            t.HasCheckConstraint("ck_cited_spend_completion", "(state = 'Settled' AND actual_usd IS NOT NULL AND completed_at_utc IS NOT NULL) OR (state IN ('Cancelled','HeldUnknown') AND actual_usd IS NULL AND completed_at_utc IS NOT NULL) OR (state IN ('Reserved','Dispatched','Uncertain') AND actual_usd IS NULL AND completed_at_utc IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.BudgetId).HasColumnName("budget_id");
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        b.Property(x => x.RequestSha256).HasColumnName("request_sha256").HasMaxLength(64);
        b.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(100);
        b.Property(x => x.Model).HasColumnName("model").HasMaxLength(100);
        b.Property(x => x.ModelVersion).HasColumnName("model_version").HasMaxLength(100);
        b.Property(x => x.RateVersion).HasColumnName("rate_version").HasMaxLength(100);
        b.Property(x => x.RateSnapshotJson).HasColumnName("rate_snapshot_json").HasColumnType("jsonb");
        b.Property(x => x.RateValidUntilUtc).HasColumnName("rate_valid_until_utc");
        b.Property(x => x.MaximumInputTokens).HasColumnName("maximum_input_tokens");
        b.Property(x => x.MaximumOutputTokens).HasColumnName("maximum_output_tokens");
        b.Property(x => x.Workload).HasColumnName("workload").HasConversion<string>().HasMaxLength(16)
            .HasDefaultValue(CitedSpendWorkload.Generation);
        b.Property(x => x.ReservedUsd).HasColumnName("reserved_usd").HasPrecision(18, 6);
        b.Property(x => x.ActualUsd).HasColumnName("actual_usd").HasPrecision(18, 6);
        b.Property(x => x.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        b.HasOne<CitedPilotBudget>().WithMany().HasForeignKey(x => x.BudgetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ActorId, x.OperationId }).IsUnique().HasDatabaseName("ux_cited_spend_operation");
        b.HasIndex(x => x.BudgetId).IsUnique().HasFilter("state IN ('Reserved','Dispatched','Uncertain')")
            .HasDatabaseName("ux_cited_spend_one_pending");
    }
}

internal sealed class CitedSpendEventConfiguration : IEntityTypeConfiguration<CitedSpendEvent>
{
    public void Configure(EntityTypeBuilder<CitedSpendEvent> b)
    {
        b.ToTable("cited_spend_events", t =>
        {
            t.HasCheckConstraint("ck_cited_spend_event_sequence", "sequence > 0");
            t.HasCheckConstraint("ck_cited_spend_event_state", "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled','HeldUnknown')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReservationId).HasColumnName("reservation_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.Sequence).HasColumnName("sequence");
        b.Property(x => x.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ActualUsd).HasColumnName("actual_usd").HasPrecision(18, 6);
        b.Property(x => x.EvidenceReference).HasColumnName("evidence_reference").HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        b.HasOne<CitedSpendReservation>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ReservationId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ReservationId, x.Sequence }).IsUnique().HasDatabaseName("ux_cited_spend_event_sequence");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ReviewSessionConfiguration : IEntityTypeConfiguration<ReviewSession>
{
    public void Configure(EntityTypeBuilder<ReviewSession> b)
    {
        b.ToTable("review_sessions", t =>
        {
            t.HasCheckConstraint("ck_review_sessions_revision", "revision_number > 0");
            t.HasCheckConstraint("ck_review_sessions_status", "status IN ('Pending','InProgress','Completed','Cancelled')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id, x.ExtractionRunId });
        b.HasAlternateKey(x => new { x.OrganizationId, x.DocumentId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.AssignedActorId).HasColumnName("assigned_actor_id");
        b.Property(x => x.SupersedesReviewSessionId).HasColumnName("supersedes_review_session_id");
        b.Property(x => x.RevisionNumber).HasColumnName("revision_number");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<Document>().WithMany(x => x.ReviewSessions)
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExtractionRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.AssignedActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewSession>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.SupersedesReviewSessionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId, x.RevisionNumber }).IsUnique()
            .HasDatabaseName("ux_review_sessions_org_document_revision");
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.StartedAtUtc }).HasDatabaseName("ix_review_sessions_org_status_started");
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId }).IsUnique()
            .HasFilter("status IN ('Pending','InProgress')").HasDatabaseName("ux_review_sessions_one_active");
        b.Navigation(x => x.Actions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReviewActionConfiguration : IEntityTypeConfiguration<ReviewAction>
{
    public void Configure(EntityTypeBuilder<ReviewAction> b)
    {
        b.ToTable("review_actions", t =>
        {
            t.HasCheckConstraint("ck_review_actions_sequence", "sequence_number > 0");
            t.HasCheckConstraint("ck_review_actions_occurrence", "occurrence_index >= 0");
            t.HasCheckConstraint("ck_review_actions_action", "action IN ('Confirmed','Corrected','Added','Cleared')");
            t.HasCheckConstraint("ck_review_actions_value_type", "corrected_value_type IS NULL OR corrected_value_type IN ('String','Decimal','Date','Currency','Identifier')");
            t.HasCheckConstraint("ck_review_actions_matrix", "(action = 'Confirmed' AND machine_field_value_id IS NOT NULL AND corrected_value_text IS NULL AND corrected_value_type IS NULL) OR (action = 'Corrected' AND machine_field_value_id IS NOT NULL AND corrected_value_text IS NOT NULL AND corrected_value_type IS NOT NULL AND reason IS NOT NULL) OR (action = 'Added' AND machine_field_value_id IS NULL AND corrected_value_text IS NOT NULL AND corrected_value_type IS NOT NULL AND reason IS NOT NULL) OR (action = 'Cleared' AND corrected_value_text IS NULL AND corrected_value_type IS NULL AND reason IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReviewSessionId).HasColumnName("review_session_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.MachineFieldValueId).HasColumnName("machine_field_value_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index").HasDefaultValue(0);
        b.Property(x => x.SequenceNumber).HasColumnName("sequence_number");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.CorrectedValueType).HasColumnName("corrected_value_type").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.CorrectedValueText).HasColumnName("corrected_value_text");
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReviewSession>().WithMany(x => x.Actions)
            .HasForeignKey(x => new { x.OrganizationId, x.ReviewSessionId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id, x.ExtractionRunId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MachineFieldValue>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.MachineFieldValueId, x.FieldKey, x.OccurrenceIndex })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.Id, x.FieldKey, x.OccurrenceIndex })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReviewSessionId, x.SequenceNumber }).IsUnique()
            .HasDatabaseName("ux_review_actions_org_session_sequence");
        b.HasIndex(x => new { x.OrganizationId, x.ReviewSessionId, x.FieldKey, x.OccurrenceIndex, x.CreatedAtUtc })
            .HasDatabaseName("ix_review_actions_effective_value");
    }
}

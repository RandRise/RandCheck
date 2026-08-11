using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ExtractionRunConfiguration : IEntityTypeConfiguration<ExtractionRun>
{
    public void Configure(EntityTypeBuilder<ExtractionRun> b)
    {
        b.ToTable("extraction_runs", t =>
        {
            t.HasCheckConstraint("ck_extraction_runs_number", "run_number > 0");
            t.HasCheckConstraint("ck_extraction_runs_duration", "duration_ms IS NULL OR duration_ms >= 0");
            t.HasCheckConstraint("ck_extraction_runs_status", "status IN ('Queued','Running','Succeeded','Partial','Failed','Cancelled')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.DocumentId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.RunNumber).HasColumnName("run_number");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(50);
        b.Property(x => x.Language).HasColumnName("language").HasMaxLength(20);
        b.Property(x => x.ProviderName).HasColumnName("provider_name").HasMaxLength(100);
        b.Property(x => x.ProviderVersion).HasColumnName("provider_version").HasMaxLength(100);
        b.Property(x => x.ModelVersion).HasColumnName("model_version").HasMaxLength(100);
        b.Property(x => x.ExtractionSchemaVersion).HasColumnName("extraction_schema_version").HasMaxLength(50);
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.DurationMs).HasColumnName("duration_ms");
        b.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(100);
        b.Property(x => x.SafeFailureSummary).HasColumnName("safe_failure_summary").HasMaxLength(500);
        b.HasOne<Document>().WithMany(x => x.ExtractionRuns)
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId, x.RunNumber }).IsUnique().HasDatabaseName("ux_extraction_runs_org_document_number");
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.StartedAtUtc }).HasDatabaseName("ix_extraction_runs_org_status_started");
        b.Navigation(x => x.Fields).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MachineFieldValueConfiguration : IEntityTypeConfiguration<MachineFieldValue>
{
    public void Configure(EntityTypeBuilder<MachineFieldValue> b)
    {
        b.ToTable("machine_field_values", t =>
        {
            t.HasCheckConstraint("ck_machine_fields_occurrence", "occurrence_index >= 0");
            t.HasCheckConstraint("ck_machine_fields_confidence", "confidence >= 0 AND confidence <= 1");
            t.HasCheckConstraint("ck_machine_fields_value_type", "value_type IN ('String','Decimal','Date','Currency','Identifier')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ExtractionRunId, x.Id, x.FieldKey, x.OccurrenceIndex });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index").HasDefaultValue(0);
        b.Property(x => x.ValueType).HasColumnName("value_type").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ValueText).HasColumnName("value_text");
        b.Property(x => x.NormalizedValueText).HasColumnName("normalized_value_text");
        b.Property(x => x.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ExtractionRun>().WithMany(x => x.Fields)
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OrganizationId, x.ExtractionRunId, x.FieldKey, x.OccurrenceIndex })
            .IsUnique().HasDatabaseName("ux_machine_fields_org_run_key_occurrence");
        b.Navigation(x => x.Evidence).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FieldEvidenceConfiguration : IEntityTypeConfiguration<FieldEvidence>
{
    public void Configure(EntityTypeBuilder<FieldEvidence> b)
    {
        b.ToTable("field_evidence", t =>
        {
            t.HasCheckConstraint("ck_field_evidence_page", "page_number >= 1");
            t.HasCheckConstraint("ck_field_evidence_offsets", "(character_start IS NULL AND character_end IS NULL) OR (character_start >= 0 AND character_end >= character_start)");
            t.HasCheckConstraint("ck_field_evidence_polygon_unit", "bounding_polygon IS NULL OR coordinate_unit IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.MachineFieldValueId).HasColumnName("machine_field_value_id");
        b.Property(x => x.PageNumber).HasColumnName("page_number");
        b.Property(x => x.QuotedText).HasColumnName("quoted_text");
        b.Property(x => x.CharacterStart).HasColumnName("character_start");
        b.Property(x => x.CharacterEnd).HasColumnName("character_end");
        b.Property(x => x.BoundingPolygon).HasColumnName("bounding_polygon");
        b.Property(x => x.CoordinateUnit).HasColumnName("coordinate_unit").HasMaxLength(16);
        b.HasOne<MachineFieldValue>().WithMany(x => x.Evidence)
            .HasForeignKey(x => new { x.OrganizationId, x.MachineFieldValueId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OrganizationId, x.MachineFieldValueId, x.PageNumber })
            .HasDatabaseName("ix_field_evidence_org_field_page");
    }
}

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
            t.HasCheckConstraint("ck_extraction_runs_provider_duration", "provider_duration_ms IS NULL OR provider_duration_ms >= 0");
            t.HasCheckConstraint("ck_extraction_runs_page_count", "page_count IS NULL OR (page_count >= 1 AND page_count <= 50)");
            t.HasCheckConstraint("ck_extraction_runs_cost", "(estimated_cost IS NULL AND cost_currency IS NULL AND price_basis IS NULL) OR (estimated_cost >= 0 AND cost_currency IS NOT NULL)");
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
        b.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(100);
        b.Property(x => x.ModelVersion).HasColumnName("model_version").HasMaxLength(100);
        b.Property(x => x.ExtractionSchemaVersion).HasColumnName("extraction_schema_version").HasMaxLength(50);
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.DurationMs).HasColumnName("duration_ms");
        b.Property(x => x.ProviderDurationMs).HasColumnName("provider_duration_ms");
        b.Property(x => x.PageCount).HasColumnName("page_count");
        b.Property(x => x.EstimatedCost).HasColumnName("estimated_cost").HasPrecision(18, 6);
        b.Property(x => x.CostCurrency).HasColumnName("cost_currency").HasMaxLength(3);
        b.Property(x => x.PriceBasis).HasColumnName("price_basis").HasMaxLength(100);
        b.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(100);
        b.Property(x => x.SafeFailureSummary).HasColumnName("safe_failure_summary").HasMaxLength(500);
        b.HasOne<Document>().WithMany(x => x.ExtractionRuns)
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.DocumentId, x.RunNumber }).IsUnique().HasDatabaseName("ux_extraction_runs_org_document_number");
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.StartedAtUtc }).HasDatabaseName("ix_extraction_runs_org_status_started");
        b.Navigation(x => x.Pages).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Fields).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Warnings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ExtractionPageConfiguration : IEntityTypeConfiguration<ExtractionPage>
{
    public void Configure(EntityTypeBuilder<ExtractionPage> b)
    {
        b.ToTable("extraction_pages", t =>
        {
            // New normalized results are capped by the domain. Persistence permits larger legacy
            // page numbers so Phase 1 data can be upgraded without falsifying or discarding citations.
            t.HasCheckConstraint("ck_extraction_pages_number", "page_number >= 1");
            t.HasCheckConstraint("ck_extraction_pages_rotation", "rotation_degrees IN (0,90,180,270)");
            t.HasCheckConstraint("ck_extraction_pages_text_bytes", "octet_length(canonical_text) <= 1048576");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber })
            .HasName("ak_extraction_pages_org_run_number");
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.PageNumber).HasColumnName("page_number");
        b.Property(x => x.CanonicalText).HasColumnName("canonical_text");
        b.Property(x => x.RotationDegrees).HasColumnName("rotation_degrees");
        b.HasOne<ExtractionRun>().WithMany(x => x.Pages)
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MachineFieldValueConfiguration : IEntityTypeConfiguration<MachineFieldValue>
{
    public void Configure(EntityTypeBuilder<MachineFieldValue> b)
    {
        b.ToTable("machine_field_values", t =>
        {
            t.HasCheckConstraint("ck_machine_fields_occurrence", "occurrence_index >= 0");
            t.HasCheckConstraint("ck_machine_fields_confidence", "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");
            t.HasCheckConstraint("ck_machine_fields_value_type", "value_type IN ('String','Decimal','Date','Currency','Identifier')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ExtractionRunId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ExtractionRunId, x.Id, x.FieldKey, x.OccurrenceIndex });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ExtractionRunId, x.FieldKey, x.OccurrenceIndex })
            .HasName("ak_machine_fields_org_run_key_occurrence");
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index").HasDefaultValue(0);
        b.Property(x => x.ValueType).HasColumnName("value_type").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ValueText).HasColumnName("value_text");
        b.Property(x => x.NormalizedValueText).HasColumnName("normalized_value_text");
        b.Property(x => x.Confidence).HasColumnName("confidence").HasPrecision(5, 4).IsRequired(false);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ExtractionRun>().WithMany(x => x.Fields)
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
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
            t.HasCheckConstraint("ck_field_evidence_offsets", "(byte_start IS NULL AND byte_end IS NULL) OR (byte_start >= 0 AND byte_end >= byte_start)");
            t.HasCheckConstraint("ck_field_evidence_quote_offsets", "(quoted_text IS NULL AND byte_start IS NULL AND byte_end IS NULL) OR (quoted_text IS NOT NULL AND byte_start IS NOT NULL AND byte_end IS NOT NULL)");
            t.HasCheckConstraint("ck_field_evidence_polygon_unit", "(bounding_polygon IS NULL AND coordinate_unit IS NULL) OR (bounding_polygon IS NOT NULL AND coordinate_unit = 'normalized')");
            t.HasCheckConstraint("ck_field_evidence_polygon_shape", "bounding_polygon IS NULL OR (jsonb_typeof(bounding_polygon) = 'array' AND jsonb_array_length(bounding_polygon) = 4)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.MachineFieldValueId).HasColumnName("machine_field_value_id");
        b.Property(x => x.PageNumber).HasColumnName("page_number");
        b.Property(x => x.QuotedText).HasColumnName("quoted_text");
        b.Property(x => x.ByteStart).HasColumnName("byte_start");
        b.Property(x => x.ByteEnd).HasColumnName("byte_end");
        b.Property(x => x.BoundingPolygon).HasColumnName("bounding_polygon").HasColumnType("jsonb");
        b.Property(x => x.CoordinateUnit).HasColumnName("coordinate_unit").HasMaxLength(16);
        b.HasOne<MachineFieldValue>().WithMany(x => x.Evidence)
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.MachineFieldValueId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ExtractionPage>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber })
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OrganizationId, x.MachineFieldValueId, x.PageNumber })
            .HasDatabaseName("ix_field_evidence_org_field_page");
    }
}

internal sealed class ExtractionWarningConfiguration : IEntityTypeConfiguration<ExtractionWarning>
{
    public void Configure(EntityTypeBuilder<ExtractionWarning> b)
    {
        b.ToTable("extraction_warnings", t =>
        {
            t.HasCheckConstraint("ck_extraction_warnings_code", "code ~ '^[a-z][a-z0-9_]{0,99}$'");
            t.HasCheckConstraint("ck_extraction_warnings_severity", "severity IN ('Info','Degraded')");
            t.HasCheckConstraint("ck_extraction_warnings_page", "page_number IS NULL OR page_number >= 1");
            t.HasCheckConstraint("ck_extraction_warnings_field_scope", "(field_key IS NULL AND occurrence_index IS NULL) OR (field_key IS NOT NULL AND occurrence_index IS NOT NULL AND occurrence_index >= 0)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100);
        b.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.SafeSummary).HasColumnName("safe_summary").HasMaxLength(300);
        b.Property(x => x.PageNumber).HasColumnName("page_number");
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index");
        b.HasOne<ExtractionRun>().WithMany(x => x.Warnings)
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ExtractionPage>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.PageNumber })
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<MachineFieldValue>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ExtractionRunId, x.FieldKey, x.OccurrenceIndex })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ExtractionRunId, x.FieldKey, x.OccurrenceIndex })
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OrganizationId, x.ExtractionRunId, x.Code, x.Severity })
            .IsUnique().HasFilter("page_number IS NULL AND field_key IS NULL")
            .HasDatabaseName("ux_extraction_warnings_global");
        b.HasIndex(x => new { x.OrganizationId, x.ExtractionRunId, x.Code, x.Severity, x.PageNumber })
            .IsUnique().HasFilter("page_number IS NOT NULL AND field_key IS NULL")
            .HasDatabaseName("ux_extraction_warnings_page");
        b.HasIndex(x => new { x.OrganizationId, x.ExtractionRunId, x.Code, x.Severity, x.FieldKey, x.OccurrenceIndex })
            .IsUnique().HasFilter("page_number IS NULL AND field_key IS NOT NULL")
            .HasDatabaseName("ux_extraction_warnings_field");
        b.HasIndex(x => new { x.OrganizationId, x.ExtractionRunId, x.Code, x.Severity,
                x.PageNumber, x.FieldKey, x.OccurrenceIndex })
            .IsUnique().HasFilter("page_number IS NOT NULL AND field_key IS NOT NULL")
            .HasDatabaseName("ux_extraction_warnings_page_field");
    }
}

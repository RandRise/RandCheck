using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class ReconciliationCaseConfiguration : IEntityTypeConfiguration<ReconciliationCase>
{
    public void Configure(EntityTypeBuilder<ReconciliationCase> b)
    {
        b.ToTable("reconciliation_cases", table => {
            table.HasCheckConstraint("ck_reconciliation_cases_status",
                "status IN ('Draft','Evaluated','Closed')");
            table.HasCheckConstraint("ck_reconciliation_cases_workflow", "workflow_kind IN ('Legacy','InvoicePurchaseOrder')");
            table.HasCheckConstraint("ck_reconciliation_cases_drafts", "(workflow_kind = 'InvoicePurchaseOrder' OR (draft_invoice_document_id IS NULL AND draft_purchase_order_document_id IS NULL AND draft_invoice_version IS NULL AND draft_purchase_order_version IS NULL)) AND (draft_invoice_document_id IS NULL OR draft_purchase_order_document_id IS NULL OR draft_invoice_document_id <> draft_purchase_order_document_id) AND (draft_invoice_document_id IS NULL OR draft_invoice_version IS NOT NULL) AND (draft_purchase_order_document_id IS NULL OR draft_purchase_order_version IS NOT NULL)");
            table.HasCheckConstraint("ck_reconciliation_cases_category", "category IN ('Uncategorized','OfficeSupplies','Services','Equipment')");
            table.HasCheckConstraint("ck_reconciliation_cases_reference", "reference ~ '^REC-[0-9]{4}-[0-9]{6,19}$'");
            table.HasCheckConstraint("ck_reconciliation_cases_creation_request", "(creation_request_key IS NULL AND creation_payload_hash IS NULL) OR (creation_request_key IS NOT NULL AND length(btrim(creation_request_key)) > 0 AND creation_payload_hash IS NOT NULL AND creation_payload_hash ~ '^[0-9a-f]{64}$')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.CreatedByActorId).HasColumnName("created_by_actor_id");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedByActorId });
        b.Property(x => x.CreationRequestKey).HasColumnName("creation_request_key").HasMaxLength(100)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.Property(x => x.CreationPayloadHash).HasColumnName("creation_payload_hash").HasMaxLength(64)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.HasIndex(x => new { x.OrganizationId, x.CreatedByActorId, x.CreationRequestKey }).IsUnique()
            .HasFilter("creation_request_key IS NOT NULL").HasDatabaseName("ux_reconciliation_cases_creation_request");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.WorkflowKind).HasColumnName("workflow_kind").HasConversion<string>().HasMaxLength(32).HasDefaultValue(ReconciliationWorkflowKind.Legacy)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.Property(x => x.DraftInvoiceDocumentId).HasColumnName("draft_invoice_document_id");
        b.Property(x => x.DraftPurchaseOrderDocumentId).HasColumnName("draft_purchase_order_document_id");
        b.Property(x => x.DraftInvoiceVersion).HasColumnName("draft_invoice_version");
        b.Property(x => x.DraftPurchaseOrderVersion).HasColumnName("draft_purchase_order_version");
        b.HasIndex(x => new { x.OrganizationId, x.WorkflowKind, x.CreatedAtUtc, x.Id }).HasDatabaseName("ix_reconciliation_cases_workflow_created");
        b.HasOne<Document>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DraftInvoiceDocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Document>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.DraftPurchaseOrderDocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Category).HasColumnName("category").HasConversion<string>().HasMaxLength(32).HasDefaultValue(ReconciliationCaseCategory.Uncategorized);
        var reference = b.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(28).ValueGeneratedOnAdd();
        reference.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        reference.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.HasIndex(x => new { x.OrganizationId, x.Reference }).IsUnique().HasDatabaseName("ux_reconciliation_cases_org_reference");
        b.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc, x.Id }).HasDatabaseName("ix_reconciliation_cases_org_created_id");
        b.HasIndex(x => new { x.OrganizationId, x.Category, x.CreatedAtUtc }).HasDatabaseName("ix_reconciliation_cases_org_category_created");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.ClosedAtUtc).HasColumnName("closed_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Actor>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.CreatedByActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.Status, x.UpdatedAtUtc })
            .HasDatabaseName("ix_reconciliation_cases_org_status_updated");
        b.Navigation(x => x.DocumentLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Runs).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReconciliationDocumentLinkConfiguration : IEntityTypeConfiguration<ReconciliationDocumentLink>
{
    public void Configure(EntityTypeBuilder<ReconciliationDocumentLink> b)
    {
        b.ToTable("reconciliation_document_links", table =>
        {
            table.HasCheckConstraint("ck_reconciliation_document_links_role",
                "role IN ('Quotation','PurchaseOrder','Invoice')");
            table.HasCheckConstraint("ck_reconciliation_document_links_sha256",
                "sha256 ~ '^[0-9a-f]{64}$'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReconciliationCaseId).HasColumnName("reconciliation_case_id");
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ReviewSessionId).HasColumnName("review_session_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        b.Property(x => x.LinkedAtUtc).HasColumnName("linked_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationCase>().WithMany(x => x.DocumentLinks)
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationCaseId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Document>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewSession>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ReviewSessionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExtractionRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationCaseId, x.Role }).IsUnique()
            .HasDatabaseName("ux_reconciliation_links_org_case_role");
    }
}

internal sealed class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> b)
    {
        b.ToTable("reconciliation_runs", table =>
        {
            table.HasCheckConstraint("ck_reconciliation_runs_number", "run_number > 0");
            table.HasCheckConstraint("ck_reconciliation_runs_status", "status IN ('Running','Completed')");
            table.HasCheckConstraint("ck_reconciliation_runs_pair_context",
                "(rule_set_version NOT IN ('invoice-po-1.0', 'invoice-po-2.0') AND order_scope IS NULL AND evaluation_actor_id IS NULL) OR (rule_set_version = 'invoice-po-1.0' AND order_scope IS NOT NULL AND order_scope IN ('Unknown','FullOrder','Partial') AND evaluation_actor_id IS NOT NULL) OR (rule_set_version = 'invoice-po-2.0' AND order_scope = 'Unknown' AND order_scope IS NOT NULL AND evaluation_actor_id IS NOT NULL)");
            table.HasCheckConstraint("ck_reconciliation_runs_tolerances",
                "quantity_tolerance >= 0 AND unit_price_tolerance >= 0 AND tax_tolerance >= 0 AND line_total_tolerance >= 0 AND document_total_tolerance >= 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ReconciliationCaseId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReconciliationCaseId).HasColumnName("reconciliation_case_id");
        b.Property(x => x.RunNumber).HasColumnName("run_number");
        b.Property(x => x.RuleSetVersion).HasColumnName("rule_set_version").HasMaxLength(50);
        b.Property(x => x.OrderScope).HasColumnName("order_scope").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.EvaluationActorId).HasColumnName("evaluation_actor_id");
        b.Property<decimal?>("CreationTransactionId").HasColumnName("creation_transaction_id")
            .HasColumnType("numeric(20,0)").HasDefaultValueSql("(pg_catalog.pg_current_xact_id()::text::numeric)")
            .ValueGeneratedOnAdd();
        b.HasOne<Actor>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.EvaluationActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.QuantityTolerance).HasColumnName("quantity_tolerance").HasPrecision(18, 6);
        b.Property(x => x.UnitPriceTolerance).HasColumnName("unit_price_tolerance").HasPrecision(18, 6);
        b.Property(x => x.TaxTolerance).HasColumnName("tax_tolerance").HasPrecision(18, 6);
        b.Property(x => x.LineTotalTolerance).HasColumnName("line_total_tolerance").HasPrecision(18, 6);
        b.Property(x => x.DocumentTotalTolerance).HasColumnName("document_total_tolerance").HasPrecision(18, 6);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationCase>().WithMany(x => x.Runs)
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationCaseId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationCaseId, x.RunNumber }).IsUnique()
            .HasDatabaseName("ux_reconciliation_runs_org_case_number");
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationCaseId }).IsUnique()
            .HasFilter("status = 'Running'").HasDatabaseName("ux_reconciliation_runs_one_running");
        b.Navigation(x => x.DocumentSnapshots).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Findings).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Checks).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReconciliationDocumentSnapshotConfiguration : IEntityTypeConfiguration<ReconciliationDocumentSnapshot>
{
    public void Configure(EntityTypeBuilder<ReconciliationDocumentSnapshot> b)
    {
        b.ToTable("reconciliation_document_snapshots", table =>
        {
            table.HasCheckConstraint("ck_reconciliation_document_snapshots_role",
                "role IN ('Quotation','PurchaseOrder','Invoice')");
            table.HasCheckConstraint("ck_reconciliation_document_snapshots_sha256",
                "sha256 ~ '^[0-9a-f]{64}$'");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReconciliationRunId).HasColumnName("reconciliation_run_id");
        b.Property(x => x.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.SourceOutcome).HasColumnName("source_outcome").HasMaxLength(32);
        b.Property(x => x.SourceProvider).HasColumnName("source_provider").HasMaxLength(100);
        b.Property(x => x.SourceAdapterVersion).HasColumnName("source_adapter_version").HasMaxLength(100);
        b.Property(x => x.SourceModelName).HasColumnName("source_model_name").HasMaxLength(100);
        b.Property(x => x.SourceModelVersion).HasColumnName("source_model_version").HasMaxLength(100);
        b.Property(x => x.SourceWarningsJson).HasColumnName("source_warnings_json").HasColumnType("jsonb");
        b.Property(x => x.ExtractedRowCount).HasColumnName("extracted_row_count");
        b.Property(x => x.RowCoverageStatus).HasColumnName("row_coverage_status").HasMaxLength(16);
        b.Property(x => x.DocumentId).HasColumnName("document_id");
        b.Property(x => x.ReviewSessionId).HasColumnName("review_session_id");
        b.Property(x => x.ExtractionRunId).HasColumnName("extraction_run_id");
        b.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationRun>().WithMany(x => x.DocumentSnapshots)
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Document>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewSession>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ReviewSessionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ExtractionRun>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.DocumentId, x.ExtractionRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.DocumentId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationRunId, x.Role }).IsUnique()
            .HasDatabaseName("ux_reconciliation_snapshots_org_run_role");
        b.Navigation(x => x.Fields).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ReconciliationFieldSnapshotConfiguration : IEntityTypeConfiguration<ReconciliationFieldSnapshot>
{
    public void Configure(EntityTypeBuilder<ReconciliationFieldSnapshot> b)
    {
        b.ToTable("reconciliation_field_snapshots", table =>
        {
            table.HasCheckConstraint("ck_reconciliation_field_snapshots_occurrence", "occurrence_index >= 0");
            table.HasCheckConstraint("ck_reconciliation_field_snapshots_value_type",
                "value_type IN ('String','Decimal','Date','Currency','Identifier')");
            table.HasCheckConstraint("ck_reconciliation_field_snapshots_source",
                "machine_field_value_id IS NOT NULL OR review_action_id IS NOT NULL");
            table.HasCheckConstraint("ck_reconciliation_field_snapshots_source_kind",
                "source_kind IS NULL OR source_kind IN ('Unchecked','Confirmed','Corrected','Added','Cleared')");
            table.HasCheckConstraint("ck_reconciliation_field_snapshots_evidence",
                "(page_number IS NULL AND quoted_text IS NULL) OR page_number > 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReconciliationRunId).HasColumnName("reconciliation_run_id");
        b.Property(x => x.DocumentSnapshotId).HasColumnName("document_snapshot_id");
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index");
        b.Property(x => x.ValueType).HasColumnName("value_type").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.SourceKind).HasColumnName("source_kind").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.ValueText).HasColumnName("value_text");
        b.Property(x => x.MachineFieldValueId).HasColumnName("machine_field_value_id");
        b.Property(x => x.ReviewActionId).HasColumnName("review_action_id");
        b.Property(x => x.PageNumber).HasColumnName("page_number");
        b.Property(x => x.QuotedText).HasColumnName("quoted_text");
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationDocumentSnapshot>().WithMany(x => x.Fields)
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.DocumentSnapshotId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id })
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<MachineFieldValue>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.MachineFieldValueId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReviewAction>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReviewActionId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationRunId,
            x.DocumentSnapshotId, x.FieldKey, x.OccurrenceIndex }).IsUnique()
            .HasDatabaseName("ux_reconciliation_fields_org_run_document_key_occurrence");
    }
}

internal sealed class ReconciliationFindingConfiguration : IEntityTypeConfiguration<ReconciliationFinding>
{
    public void Configure(EntityTypeBuilder<ReconciliationFinding> b)
    {
        b.ToTable("reconciliation_findings", table =>
        {
            table.HasCheckConstraint("ck_reconciliation_findings_severity", "severity IN ('Warning','Error')");
            table.HasCheckConstraint("ck_reconciliation_findings_roles", "left_role IS NOT NULL OR right_role IS NOT NULL");
            table.HasCheckConstraint("ck_reconciliation_findings_occurrence", "occurrence_index IS NULL OR occurrence_index >= 0");
            table.HasCheckConstraint("ck_reconciliation_findings_numeric",
                "(difference IS NULL OR difference >= 0) AND (tolerance IS NULL OR (tolerance >= 0 AND difference IS NOT NULL))");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.OrganizationId, x.Id });
        b.HasAlternateKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.ReconciliationRunId).HasColumnName("reconciliation_run_id");
        b.Property(x => x.RuleCode).HasColumnName("rule_code").HasMaxLength(50);
        b.Property(x => x.RuleVersion).HasColumnName("rule_version").HasMaxLength(50);
        b.Property(x => x.Severity).HasColumnName("severity").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.LeftRole).HasColumnName("left_role").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.RightRole).HasColumnName("right_role").HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.LeftFieldSnapshotId).HasColumnName("left_field_snapshot_id");
        b.Property(x => x.RightFieldSnapshotId).HasColumnName("right_field_snapshot_id");
        b.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100);
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index");
        b.Property(x => x.LeftValue).HasColumnName("left_value");
        b.Property(x => x.RightValue).HasColumnName("right_value");
        b.Property(x => x.Difference).HasColumnName("difference").HasPrecision(24, 6);
        b.Property(x => x.Tolerance).HasColumnName("tolerance").HasPrecision(18, 6);
        b.Property(x => x.Explanation).HasColumnName("explanation").HasMaxLength(1000);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationRun>().WithMany(x => x.Findings)
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ReconciliationFieldSnapshot>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.LeftFieldSnapshotId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReconciliationFieldSnapshot>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.RightFieldSnapshotId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.ReconciliationRunId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.ReconciliationRunId, x.RuleCode,
            x.LeftRole, x.RightRole, x.FieldKey, x.OccurrenceIndex })
            .HasDatabaseName("ix_reconciliation_findings_identity");
        b.Navigation(x => x.Resolutions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FindingResolutionConfiguration : IEntityTypeConfiguration<FindingResolution>
{
    public void Configure(EntityTypeBuilder<FindingResolution> b)
    {
        b.ToTable("finding_resolutions", table =>
        {
            table.HasCheckConstraint("ck_finding_resolutions_sequence", "sequence_number > 0");
            table.HasCheckConstraint("ck_finding_resolutions_action",
                "action IN ('Resolved','Accepted','Dismissed')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.OrganizationId).HasColumnName("organization_id");
        b.Property(x => x.FindingId).HasColumnName("finding_id");
        b.Property(x => x.ActorId).HasColumnName("actor_id");
        b.Property(x => x.SequenceNumber).HasColumnName("sequence_number");
        b.Property(x => x.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        b.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        b.HasOne<ReconciliationFinding>().WithMany(x => x.Resolutions)
            .HasForeignKey(x => new { x.OrganizationId, x.FindingId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Actor>().WithMany()
            .HasForeignKey(x => new { x.OrganizationId, x.ActorId })
            .HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OrganizationId, x.FindingId, x.SequenceNumber }).IsUnique()
            .HasDatabaseName("ux_finding_resolutions_org_finding_sequence");
    }
}

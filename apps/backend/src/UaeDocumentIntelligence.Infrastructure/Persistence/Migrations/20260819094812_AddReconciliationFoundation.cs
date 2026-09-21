using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_review_sessions_organization_id_id",
                table: "review_sessions",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_review_actions_organization_id_id",
                table: "review_actions",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateTable(
                name: "reconciliation_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_cases", x => x.id);
                    table.UniqueConstraint("AK_reconciliation_cases_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_reconciliation_cases_status", "status IN ('Draft','Evaluated','Closed')");
                    table.ForeignKey(
                        name: "FK_reconciliation_cases_actors_organization_id_created_by_acto~",
                        columns: x => new { x.organization_id, x.created_by_actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_cases_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_document_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    linked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_document_links", x => x.id);
                    table.CheckConstraint("ck_reconciliation_document_links_role", "role IN ('Quotation','PurchaseOrder','Invoice')");
                    table.CheckConstraint("ck_reconciliation_document_links_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_reconciliation_document_links_documents_organization_id_doc~",
                        columns: x => new { x.organization_id, x.document_id },
                        principalTable: "documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_document_links_extraction_runs_organization_~",
                        columns: x => new { x.organization_id, x.document_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_document_links_reconciliation_cases_organiza~",
                        columns: x => new { x.organization_id, x.reconciliation_case_id },
                        principalTable: "reconciliation_cases",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reconciliation_document_links_review_sessions_organization_~",
                        columns: x => new { x.organization_id, x.document_id, x.review_session_id },
                        principalTable: "review_sessions",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_number = table.Column<int>(type: "integer", nullable: false),
                    rule_set_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quantity_tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price_tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tax_tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    line_total_tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    document_total_tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_runs", x => x.id);
                    table.UniqueConstraint("AK_reconciliation_runs_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_reconciliation_runs_organization_id_reconciliation_case_id_~", x => new { x.organization_id, x.reconciliation_case_id, x.id });
                    table.CheckConstraint("ck_reconciliation_runs_number", "run_number > 0");
                    table.CheckConstraint("ck_reconciliation_runs_status", "status IN ('Running','Completed')");
                    table.CheckConstraint("ck_reconciliation_runs_tolerances", "quantity_tolerance >= 0 AND unit_price_tolerance >= 0 AND tax_tolerance >= 0 AND line_total_tolerance >= 0 AND document_total_tolerance >= 0");
                    table.ForeignKey(
                        name: "FK_reconciliation_runs_reconciliation_cases_organization_id_re~",
                        columns: x => new { x.organization_id, x.reconciliation_case_id },
                        principalTable: "reconciliation_cases",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_document_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_document_snapshots", x => x.id);
                    table.UniqueConstraint("AK_reconciliation_document_snapshots_organization_id_reconcili~", x => new { x.organization_id, x.reconciliation_run_id, x.id });
                    table.CheckConstraint("ck_reconciliation_document_snapshots_role", "role IN ('Quotation','PurchaseOrder','Invoice')");
                    table.CheckConstraint("ck_reconciliation_document_snapshots_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_reconciliation_document_snapshots_documents_organization_id~",
                        columns: x => new { x.organization_id, x.document_id },
                        principalTable: "documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_document_snapshots_extraction_runs_organizat~",
                        columns: x => new { x.organization_id, x.document_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_document_snapshots_reconciliation_runs_organ~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id },
                        principalTable: "reconciliation_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reconciliation_document_snapshots_review_sessions_organizat~",
                        columns: x => new { x.organization_id, x.document_id, x.review_session_id },
                        principalTable: "review_sessions",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_field_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false),
                    value_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    value_text = table.Column<string>(type: "text", nullable: false),
                    machine_field_value_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_action_id = table.Column<Guid>(type: "uuid", nullable: true),
                    page_number = table.Column<int>(type: "integer", nullable: true),
                    quoted_text = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_field_snapshots", x => x.id);
                    table.UniqueConstraint("AK_reconciliation_field_snapshots_organization_id_reconciliati~", x => new { x.organization_id, x.reconciliation_run_id, x.id });
                    table.CheckConstraint("ck_reconciliation_field_snapshots_evidence", "(page_number IS NULL AND quoted_text IS NULL) OR page_number > 0");
                    table.CheckConstraint("ck_reconciliation_field_snapshots_occurrence", "occurrence_index >= 0");
                    table.CheckConstraint("ck_reconciliation_field_snapshots_source", "machine_field_value_id IS NOT NULL OR review_action_id IS NOT NULL");
                    table.CheckConstraint("ck_reconciliation_field_snapshots_value_type", "value_type IN ('String','Decimal','Date','Currency','Identifier')");
                    table.ForeignKey(
                        name: "FK_reconciliation_field_snapshots_machine_field_values_organiz~",
                        columns: x => new { x.organization_id, x.machine_field_value_id },
                        principalTable: "machine_field_values",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_field_snapshots_reconciliation_document_snap~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id, x.document_snapshot_id },
                        principalTable: "reconciliation_document_snapshots",
                        principalColumns: new[] { "organization_id", "reconciliation_run_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reconciliation_field_snapshots_review_actions_organization_~",
                        columns: x => new { x.organization_id, x.review_action_id },
                        principalTable: "review_actions",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_findings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rule_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    left_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    right_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    left_field_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    right_field_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurrence_index = table.Column<int>(type: "integer", nullable: true),
                    left_value = table.Column<string>(type: "text", nullable: true),
                    right_value = table.Column<string>(type: "text", nullable: true),
                    difference = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_findings", x => x.id);
                    table.UniqueConstraint("AK_reconciliation_findings_organization_id_id", x => new { x.organization_id, x.id });
                    table.UniqueConstraint("AK_reconciliation_findings_organization_id_reconciliation_run_~", x => new { x.organization_id, x.reconciliation_run_id, x.id });
                    table.CheckConstraint("ck_reconciliation_findings_numeric", "(difference IS NULL OR difference >= 0) AND (tolerance IS NULL OR (tolerance >= 0 AND difference IS NOT NULL))");
                    table.CheckConstraint("ck_reconciliation_findings_occurrence", "occurrence_index IS NULL OR occurrence_index >= 0");
                    table.CheckConstraint("ck_reconciliation_findings_roles", "left_role IS NOT NULL OR right_role IS NOT NULL");
                    table.CheckConstraint("ck_reconciliation_findings_severity", "severity IN ('Warning','Error')");
                    table.ForeignKey(
                        name: "FK_reconciliation_findings_reconciliation_field_snapshots_orga~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id, x.left_field_snapshot_id },
                        principalTable: "reconciliation_field_snapshots",
                        principalColumns: new[] { "organization_id", "reconciliation_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_findings_reconciliation_field_snapshots_org~1",
                        columns: x => new { x.organization_id, x.reconciliation_run_id, x.right_field_snapshot_id },
                        principalTable: "reconciliation_field_snapshots",
                        principalColumns: new[] { "organization_id", "reconciliation_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_findings_reconciliation_runs_organization_id~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id },
                        principalTable: "reconciliation_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finding_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finding_resolutions", x => x.id);
                    table.CheckConstraint("ck_finding_resolutions_action", "action IN ('Resolved','Accepted','Dismissed')");
                    table.CheckConstraint("ck_finding_resolutions_sequence", "sequence_number > 0");
                    table.ForeignKey(
                        name: "FK_finding_resolutions_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_finding_resolutions_reconciliation_findings_organization_id~",
                        columns: x => new { x.organization_id, x.finding_id },
                        principalTable: "reconciliation_findings",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finding_resolutions_organization_id_actor_id",
                table: "finding_resolutions",
                columns: new[] { "organization_id", "actor_id" });

            migrationBuilder.CreateIndex(
                name: "ux_finding_resolutions_org_finding_sequence",
                table: "finding_resolutions",
                columns: new[] { "organization_id", "finding_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_cases_org_status_updated",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "status", "updated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_cases_organization_id_created_by_actor_id",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "created_by_actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_document_links_organization_id_document_id_e~",
                table: "reconciliation_document_links",
                columns: new[] { "organization_id", "document_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_document_links_organization_id_document_id_r~",
                table: "reconciliation_document_links",
                columns: new[] { "organization_id", "document_id", "review_session_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_links_org_case_role",
                table: "reconciliation_document_links",
                columns: new[] { "organization_id", "reconciliation_case_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_document_snapshots_organization_id_document_~",
                table: "reconciliation_document_snapshots",
                columns: new[] { "organization_id", "document_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_document_snapshots_organization_id_document~1",
                table: "reconciliation_document_snapshots",
                columns: new[] { "organization_id", "document_id", "review_session_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_snapshots_org_run_role",
                table: "reconciliation_document_snapshots",
                columns: new[] { "organization_id", "reconciliation_run_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_field_snapshots_organization_id_machine_fiel~",
                table: "reconciliation_field_snapshots",
                columns: new[] { "organization_id", "machine_field_value_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_field_snapshots_organization_id_review_actio~",
                table: "reconciliation_field_snapshots",
                columns: new[] { "organization_id", "review_action_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_fields_org_run_document_key_occurrence",
                table: "reconciliation_field_snapshots",
                columns: new[] { "organization_id", "reconciliation_run_id", "document_snapshot_id", "field_key", "occurrence_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_findings_identity",
                table: "reconciliation_findings",
                columns: new[] { "organization_id", "reconciliation_run_id", "rule_code", "left_role", "right_role", "field_key", "occurrence_index" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_findings_organization_id_reconciliation_run_~",
                table: "reconciliation_findings",
                columns: new[] { "organization_id", "reconciliation_run_id", "left_field_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_findings_organization_id_reconciliation_run~1",
                table: "reconciliation_findings",
                columns: new[] { "organization_id", "reconciliation_run_id", "right_field_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_runs_one_running",
                table: "reconciliation_runs",
                columns: new[] { "organization_id", "reconciliation_case_id" },
                unique: true,
                filter: "status = 'Running'");

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_runs_org_case_number",
                table: "reconciliation_runs",
                columns: new[] { "organization_id", "reconciliation_case_id", "run_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finding_resolutions");

            migrationBuilder.DropTable(
                name: "reconciliation_document_links");

            migrationBuilder.DropTable(
                name: "reconciliation_findings");

            migrationBuilder.DropTable(
                name: "reconciliation_field_snapshots");

            migrationBuilder.DropTable(
                name: "reconciliation_document_snapshots");

            migrationBuilder.DropTable(
                name: "reconciliation_runs");

            migrationBuilder.DropTable(
                name: "reconciliation_cases");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_review_sessions_organization_id_id",
                table: "review_sessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_review_actions_organization_id_id",
                table: "review_actions");
        }
    }
}

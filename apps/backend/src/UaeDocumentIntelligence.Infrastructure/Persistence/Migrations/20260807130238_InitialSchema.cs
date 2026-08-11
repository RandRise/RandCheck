using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "actors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    external_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actors", x => x.id);
                    table.UniqueConstraint("AK_actors_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_actors_kind", "kind IN ('Human','System')");
                    table.ForeignKey(
                        name: "FK_actors_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    declared_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    detected_content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", nullable: false),
                    processing_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.UniqueConstraint("AK_documents_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_documents_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_documents_size", "size_bytes > 0");
                    table.CheckConstraint("ck_documents_status", "processing_status IN ('Stored','Processing','ReadyForReview','ExtractionFailed')");
                    table.CheckConstraint("ck_documents_timestamps", "updated_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "FK_documents_actors_organization_id_created_by_actor_id",
                        columns: x => new { x.organization_id, x.created_by_actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "extraction_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    extraction_schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    safe_failure_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_runs", x => x.id);
                    table.UniqueConstraint("AK_extraction_runs_organization_id_document_id_id", x => new { x.organization_id, x.document_id, x.id });
                    table.UniqueConstraint("AK_extraction_runs_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_extraction_runs_duration", "duration_ms IS NULL OR duration_ms >= 0");
                    table.CheckConstraint("ck_extraction_runs_number", "run_number > 0");
                    table.CheckConstraint("ck_extraction_runs_status", "status IN ('Queued','Running','Succeeded','Partial','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_extraction_runs_documents_organization_id_document_id",
                        columns: x => new { x.organization_id, x.document_id },
                        principalTable: "documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "machine_field_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    value_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    value_text = table.Column<string>(type: "text", nullable: false),
                    normalized_value_text = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_field_values", x => x.id);
                    table.UniqueConstraint("AK_machine_field_values_organization_id_extraction_run_id_id_f~", x => new { x.organization_id, x.extraction_run_id, x.id, x.field_key, x.occurrence_index });
                    table.UniqueConstraint("AK_machine_field_values_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_machine_fields_confidence", "confidence >= 0 AND confidence <= 1");
                    table.CheckConstraint("ck_machine_fields_occurrence", "occurrence_index >= 0");
                    table.CheckConstraint("ck_machine_fields_value_type", "value_type IN ('String','Decimal','Date','Currency','Identifier')");
                    table.ForeignKey(
                        name: "FK_machine_field_values_extraction_runs_organization_id_extrac~",
                        columns: x => new { x.organization_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supersedes_review_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    concurrency_token = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_sessions", x => x.id);
                    table.UniqueConstraint("AK_review_sessions_organization_id_document_id_id", x => new { x.organization_id, x.document_id, x.id });
                    table.UniqueConstraint("AK_review_sessions_organization_id_id_extraction_run_id", x => new { x.organization_id, x.id, x.extraction_run_id });
                    table.CheckConstraint("ck_review_sessions_revision", "revision_number > 0");
                    table.CheckConstraint("ck_review_sessions_status", "status IN ('Pending','InProgress','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_review_sessions_actors_organization_id_assigned_actor_id",
                        columns: x => new { x.organization_id, x.assigned_actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_sessions_documents_organization_id_document_id",
                        columns: x => new { x.organization_id, x.document_id },
                        principalTable: "documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_sessions_extraction_runs_organization_id_document_id~",
                        columns: x => new { x.organization_id, x.document_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_sessions_review_sessions_organization_id_document_id~",
                        columns: x => new { x.organization_id, x.document_id, x.supersedes_review_session_id },
                        principalTable: "review_sessions",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "field_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_field_value_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    quoted_text = table.Column<string>(type: "text", nullable: true),
                    character_start = table.Column<int>(type: "integer", nullable: true),
                    character_end = table.Column<int>(type: "integer", nullable: true),
                    bounding_polygon = table.Column<string>(type: "text", nullable: true),
                    coordinate_unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_evidence", x => x.id);
                    table.CheckConstraint("ck_field_evidence_offsets", "(character_start IS NULL AND character_end IS NULL) OR (character_start >= 0 AND character_end >= character_start)");
                    table.CheckConstraint("ck_field_evidence_page", "page_number >= 1");
                    table.CheckConstraint("ck_field_evidence_polygon_unit", "bounding_polygon IS NULL OR coordinate_unit IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_field_evidence_machine_field_values_organization_id_machine~",
                        columns: x => new { x.organization_id, x.machine_field_value_id },
                        principalTable: "machine_field_values",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_field_value_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    corrected_value_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    corrected_value_text = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_actions", x => x.id);
                    table.CheckConstraint("ck_review_actions_action", "action IN ('Confirmed','Corrected','Added','Cleared')");
                    table.CheckConstraint("ck_review_actions_matrix", "(action = 'Confirmed' AND machine_field_value_id IS NOT NULL AND corrected_value_text IS NULL AND corrected_value_type IS NULL) OR (action = 'Corrected' AND machine_field_value_id IS NOT NULL AND corrected_value_text IS NOT NULL AND corrected_value_type IS NOT NULL AND reason IS NOT NULL) OR (action = 'Added' AND machine_field_value_id IS NULL AND corrected_value_text IS NOT NULL AND corrected_value_type IS NOT NULL AND reason IS NOT NULL) OR (action = 'Cleared' AND corrected_value_text IS NULL AND corrected_value_type IS NULL AND reason IS NOT NULL)");
                    table.CheckConstraint("ck_review_actions_occurrence", "occurrence_index >= 0");
                    table.CheckConstraint("ck_review_actions_sequence", "sequence_number > 0");
                    table.ForeignKey(
                        name: "FK_review_actions_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_actions_machine_field_values_organization_id_extract~",
                        columns: x => new { x.organization_id, x.extraction_run_id, x.machine_field_value_id, x.field_key, x.occurrence_index },
                        principalTable: "machine_field_values",
                        principalColumns: new[] { "organization_id", "extraction_run_id", "id", "field_key", "occurrence_index" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_review_actions_review_sessions_organization_id_review_sessi~",
                        columns: x => new { x.organization_id, x.review_session_id, x.extraction_run_id },
                        principalTable: "review_sessions",
                        principalColumns: new[] { "organization_id", "id", "extraction_run_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_actors_org_external_subject",
                table: "actors",
                columns: new[] { "organization_id", "external_subject" },
                unique: true,
                filter: "external_subject IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_documents_org_created",
                table: "documents",
                columns: new[] { "organization_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_org_sha256",
                table: "documents",
                columns: new[] { "organization_id", "sha256" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_org_status_created",
                table: "documents",
                columns: new[] { "organization_id", "processing_status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_organization_id_created_by_actor_id",
                table: "documents",
                columns: new[] { "organization_id", "created_by_actor_id" });

            migrationBuilder.CreateIndex(
                name: "ux_documents_storage_key",
                table: "documents",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_extraction_runs_org_status_started",
                table: "extraction_runs",
                columns: new[] { "organization_id", "status", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_extraction_runs_org_document_number",
                table: "extraction_runs",
                columns: new[] { "organization_id", "document_id", "run_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_evidence_org_field_page",
                table: "field_evidence",
                columns: new[] { "organization_id", "machine_field_value_id", "page_number" });

            migrationBuilder.CreateIndex(
                name: "ux_machine_fields_org_run_key_occurrence",
                table: "machine_field_values",
                columns: new[] { "organization_id", "extraction_run_id", "field_key", "occurrence_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_actions_effective_value",
                table: "review_actions",
                columns: new[] { "organization_id", "review_session_id", "field_key", "occurrence_index", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_review_actions_organization_id_actor_id",
                table: "review_actions",
                columns: new[] { "organization_id", "actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_review_actions_organization_id_extraction_run_id_machine_fi~",
                table: "review_actions",
                columns: new[] { "organization_id", "extraction_run_id", "machine_field_value_id", "field_key", "occurrence_index" });

            migrationBuilder.CreateIndex(
                name: "IX_review_actions_organization_id_review_session_id_extraction~",
                table: "review_actions",
                columns: new[] { "organization_id", "review_session_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "ux_review_actions_org_session_sequence",
                table: "review_actions",
                columns: new[] { "organization_id", "review_session_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_org_status_started",
                table: "review_sessions",
                columns: new[] { "organization_id", "status", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_review_sessions_organization_id_assigned_actor_id",
                table: "review_sessions",
                columns: new[] { "organization_id", "assigned_actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_review_sessions_organization_id_document_id_extraction_run_~",
                table: "review_sessions",
                columns: new[] { "organization_id", "document_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_review_sessions_organization_id_document_id_supersedes_revi~",
                table: "review_sessions",
                columns: new[] { "organization_id", "document_id", "supersedes_review_session_id" });

            migrationBuilder.CreateIndex(
                name: "ux_review_sessions_one_active",
                table: "review_sessions",
                columns: new[] { "organization_id", "document_id" },
                unique: true,
                filter: "status IN ('Pending','InProgress')");

            migrationBuilder.CreateIndex(
                name: "ux_review_sessions_org_document_revision",
                table: "review_sessions",
                columns: new[] { "organization_id", "document_id", "revision_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "field_evidence");

            migrationBuilder.DropTable(
                name: "review_actions");

            migrationBuilder.DropTable(
                name: "machine_field_values");

            migrationBuilder.DropTable(
                name: "review_sessions");

            migrationBuilder.DropTable(
                name: "extraction_runs");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "actors");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}

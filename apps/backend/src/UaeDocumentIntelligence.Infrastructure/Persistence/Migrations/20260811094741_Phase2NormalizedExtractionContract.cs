using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2NormalizedExtractionContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_machine_fields_org_run_key_occurrence",
                table: "machine_field_values");

            migrationBuilder.DropCheckConstraint(
                name: "ck_machine_fields_confidence",
                table: "machine_field_values");

            migrationBuilder.DropCheckConstraint(
                name: "ck_field_evidence_offsets",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_field_evidence_polygon_unit",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_documents_status",
                table: "documents");

            // Phase 1 did not persist canonical page text, so legacy character offsets cannot be
            // safely reinterpreted as UTF-8 byte offsets. Legacy free-form polygons are likewise
            // not guaranteed to be normalized JSON. Preserve page provenance and null ambiguous data.
            migrationBuilder.Sql(
                """
                UPDATE field_evidence
                SET quoted_text = NULL,
                    character_start = NULL,
                    character_end = NULL,
                    bounding_polygon = NULL,
                    coordinate_unit = NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "character_start",
                table: "field_evidence",
                newName: "byte_start");

            migrationBuilder.RenameColumn(
                name: "character_end",
                table: "field_evidence",
                newName: "byte_end");

            migrationBuilder.AlterColumn<decimal>(
                name: "confidence",
                table: "machine_field_values",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4);

            migrationBuilder.Sql(
                """
                ALTER TABLE field_evidence
                ALTER COLUMN bounding_polygon TYPE jsonb
                USING bounding_polygon::jsonb;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "extraction_run_id",
                table: "field_evidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cost_currency",
                table: "extraction_runs",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "estimated_cost",
                table: "extraction_runs",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model_name",
                table: "extraction_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "page_count",
                table: "extraction_runs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "price_basis",
                table: "extraction_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "provider_duration_ms",
                table: "extraction_runs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_machine_fields_org_run_key_occurrence",
                table: "machine_field_values",
                columns: new[] { "organization_id", "extraction_run_id", "field_key", "occurrence_index" });

            migrationBuilder.Sql(
                """
                UPDATE field_evidence AS evidence
                SET extraction_run_id = field.extraction_run_id
                FROM machine_field_values AS field
                WHERE field.organization_id = evidence.organization_id
                  AND field.id = evidence.machine_field_value_id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "extraction_run_id",
                table: "field_evidence",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "extraction_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    canonical_text = table.Column<string>(type: "text", nullable: false),
                    rotation_degrees = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_pages", x => x.id);
                    table.UniqueConstraint("ak_extraction_pages_org_run_number", x => new { x.organization_id, x.extraction_run_id, x.page_number });
                    table.CheckConstraint("ck_extraction_pages_number", "page_number >= 1");
                    table.CheckConstraint("ck_extraction_pages_rotation", "rotation_degrees IN (0,90,180,270)");
                    table.CheckConstraint("ck_extraction_pages_text_bytes", "octet_length(canonical_text) <= 1048576");
                    table.ForeignKey(
                        name: "FK_extraction_pages_extraction_runs_organization_id_extraction~",
                        columns: x => new { x.organization_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO extraction_pages
                    (id, organization_id, extraction_run_id, page_number, canonical_text, rotation_degrees)
                SELECT md5(organization_id::text || extraction_run_id::text || page_number::text)::uuid,
                       organization_id,
                       extraction_run_id,
                       page_number,
                       '',
                       0
                FROM field_evidence
                GROUP BY organization_id, extraction_run_id, page_number;
                """);

            migrationBuilder.CreateTable(
                name: "extraction_warnings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    safe_summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: true),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurrence_index = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_warnings", x => x.id);
                    table.CheckConstraint("ck_extraction_warnings_code", "code ~ '^[a-z][a-z0-9_]{0,99}$'");
                    table.CheckConstraint("ck_extraction_warnings_field_scope", "(field_key IS NULL AND occurrence_index IS NULL) OR (field_key IS NOT NULL AND occurrence_index >= 0)");
                    table.CheckConstraint("ck_extraction_warnings_page", "page_number IS NULL OR page_number >= 1");
                    table.CheckConstraint("ck_extraction_warnings_severity", "severity IN ('Info','Degraded')");
                    table.ForeignKey(
                        name: "FK_extraction_warnings_extraction_pages_organization_id_extrac~",
                        columns: x => new { x.organization_id, x.extraction_run_id, x.page_number },
                        principalTable: "extraction_pages",
                        principalColumns: new[] { "organization_id", "extraction_run_id", "page_number" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_extraction_warnings_extraction_runs_organization_id_extract~",
                        columns: x => new { x.organization_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_extraction_warnings_machine_field_values_organization_id_ex~",
                        columns: x => new { x.organization_id, x.extraction_run_id, x.field_key, x.occurrence_index },
                        principalTable: "machine_field_values",
                        principalColumns: new[] { "organization_id", "extraction_run_id", "field_key", "occurrence_index" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_machine_fields_confidence",
                table: "machine_field_values",
                sql: "confidence IS NULL OR (confidence >= 0 AND confidence <= 1)");

            migrationBuilder.CreateIndex(
                name: "IX_field_evidence_organization_id_extraction_run_id_page_number",
                table: "field_evidence",
                columns: new[] { "organization_id", "extraction_run_id", "page_number" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_evidence_offsets",
                table: "field_evidence",
                sql: "(byte_start IS NULL AND byte_end IS NULL) OR (byte_start >= 0 AND byte_end >= byte_start)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_evidence_polygon_shape",
                table: "field_evidence",
                sql: "bounding_polygon IS NULL OR (jsonb_typeof(bounding_polygon) = 'array' AND jsonb_array_length(bounding_polygon) = 4)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_evidence_polygon_unit",
                table: "field_evidence",
                sql: "(bounding_polygon IS NULL AND coordinate_unit IS NULL) OR (bounding_polygon IS NOT NULL AND coordinate_unit = 'normalized')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_evidence_quote_offsets",
                table: "field_evidence",
                sql: "(quoted_text IS NULL AND byte_start IS NULL AND byte_end IS NULL) OR (quoted_text IS NOT NULL AND byte_start IS NOT NULL AND byte_end IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_runs_cost",
                table: "extraction_runs",
                sql: "(estimated_cost IS NULL AND cost_currency IS NULL AND price_basis IS NULL) OR (estimated_cost >= 0 AND cost_currency IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_runs_page_count",
                table: "extraction_runs",
                sql: "page_count IS NULL OR (page_count >= 1 AND page_count <= 50)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_runs_provider_duration",
                table: "extraction_runs",
                sql: "provider_duration_ms IS NULL OR provider_duration_ms >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_documents_status",
                table: "documents",
                sql: "processing_status IN ('Stored','Processing','ReadyForReview','ExtractionFailed','ProcessingCancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_warnings_organization_id_extraction_run_id_field~",
                table: "extraction_warnings",
                columns: new[] { "organization_id", "extraction_run_id", "field_key", "occurrence_index" });

            migrationBuilder.CreateIndex(
                name: "IX_extraction_warnings_organization_id_extraction_run_id_page_~",
                table: "extraction_warnings",
                columns: new[] { "organization_id", "extraction_run_id", "page_number" });

            migrationBuilder.CreateIndex(
                name: "ux_extraction_warnings_field",
                table: "extraction_warnings",
                columns: new[] { "organization_id", "extraction_run_id", "code", "severity", "field_key", "occurrence_index" },
                unique: true,
                filter: "page_number IS NULL AND field_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_extraction_warnings_global",
                table: "extraction_warnings",
                columns: new[] { "organization_id", "extraction_run_id", "code", "severity" },
                unique: true,
                filter: "page_number IS NULL AND field_key IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_extraction_warnings_page",
                table: "extraction_warnings",
                columns: new[] { "organization_id", "extraction_run_id", "code", "severity", "page_number" },
                unique: true,
                filter: "page_number IS NOT NULL AND field_key IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_extraction_warnings_page_field",
                table: "extraction_warnings",
                columns: new[] { "organization_id", "extraction_run_id", "code", "severity", "page_number", "field_key", "occurrence_index" },
                unique: true,
                filter: "page_number IS NOT NULL AND field_key IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_field_evidence_extraction_pages_organization_id_extraction_~",
                table: "field_evidence",
                columns: new[] { "organization_id", "extraction_run_id", "page_number" },
                principalTable: "extraction_pages",
                principalColumns: new[] { "organization_id", "extraction_run_id", "page_number" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_documents_status",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_field_evidence_extraction_pages_organization_id_extraction_~",
                table: "field_evidence");

            migrationBuilder.DropTable(
                name: "extraction_warnings");

            migrationBuilder.DropTable(
                name: "extraction_pages");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_machine_fields_org_run_key_occurrence",
                table: "machine_field_values");

            migrationBuilder.DropCheckConstraint(
                name: "ck_machine_fields_confidence",
                table: "machine_field_values");

            migrationBuilder.DropIndex(
                name: "IX_field_evidence_organization_id_extraction_run_id_page_number",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_field_evidence_offsets",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_field_evidence_polygon_shape",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_field_evidence_polygon_unit",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_field_evidence_quote_offsets",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_runs_cost",
                table: "extraction_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_runs_page_count",
                table: "extraction_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_runs_provider_duration",
                table: "extraction_runs");

            migrationBuilder.DropColumn(
                name: "extraction_run_id",
                table: "field_evidence");

            migrationBuilder.DropColumn(
                name: "cost_currency",
                table: "extraction_runs");

            migrationBuilder.DropColumn(
                name: "estimated_cost",
                table: "extraction_runs");

            migrationBuilder.DropColumn(
                name: "model_name",
                table: "extraction_runs");

            migrationBuilder.DropColumn(
                name: "page_count",
                table: "extraction_runs");

            migrationBuilder.DropColumn(
                name: "price_basis",
                table: "extraction_runs");

            migrationBuilder.DropColumn(
                name: "provider_duration_ms",
                table: "extraction_runs");

            migrationBuilder.RenameColumn(
                name: "byte_start",
                table: "field_evidence",
                newName: "character_start");

            migrationBuilder.RenameColumn(
                name: "byte_end",
                table: "field_evidence",
                newName: "character_end");

            // Phase 1 required confidence. A development rollback uses the former neutral default
            // for values that were legitimately unknown under the Phase 2 contract.
            migrationBuilder.Sql("UPDATE machine_field_values SET confidence = 0 WHERE confidence IS NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "confidence",
                table: "machine_field_values",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,4)",
                oldPrecision: 5,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE field_evidence
                ALTER COLUMN bounding_polygon TYPE text
                USING bounding_polygon::text;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_machine_fields_org_run_key_occurrence",
                table: "machine_field_values",
                columns: new[] { "organization_id", "extraction_run_id", "field_key", "occurrence_index" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_machine_fields_confidence",
                table: "machine_field_values",
                sql: "confidence >= 0 AND confidence <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_evidence_offsets",
                table: "field_evidence",
                sql: "(character_start IS NULL AND character_end IS NULL) OR (character_start >= 0 AND character_end >= character_start)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_evidence_polygon_unit",
                table: "field_evidence",
                sql: "bounding_polygon IS NULL OR coordinate_unit IS NOT NULL");

            migrationBuilder.Sql(
                "UPDATE documents SET processing_status = 'Stored' WHERE processing_status = 'ProcessingCancelled';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_documents_status",
                table: "documents",
                sql: "processing_status IN ('Stored','Processing','ReadyForReview','ExtractionFailed')");
        }
    }
}

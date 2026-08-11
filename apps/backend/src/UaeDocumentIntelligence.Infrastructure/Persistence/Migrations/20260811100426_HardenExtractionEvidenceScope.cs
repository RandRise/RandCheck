using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenExtractionEvidenceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_field_evidence_machine_field_values_organization_id_machine~",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_warnings_field_scope",
                table: "extraction_warnings");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_machine_field_values_organization_id_extraction_run_id_id",
                table: "machine_field_values",
                columns: new[] { "organization_id", "extraction_run_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_field_evidence_organization_id_extraction_run_id_machine_fi~",
                table: "field_evidence",
                columns: new[] { "organization_id", "extraction_run_id", "machine_field_value_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_warnings_field_scope",
                table: "extraction_warnings",
                sql: "(field_key IS NULL AND occurrence_index IS NULL) OR (field_key IS NOT NULL AND occurrence_index IS NOT NULL AND occurrence_index >= 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_field_evidence_machine_field_values_organization_id_extract~",
                table: "field_evidence",
                columns: new[] { "organization_id", "extraction_run_id", "machine_field_value_id" },
                principalTable: "machine_field_values",
                principalColumns: new[] { "organization_id", "extraction_run_id", "id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_field_evidence_machine_field_values_organization_id_extract~",
                table: "field_evidence");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_machine_field_values_organization_id_extraction_run_id_id",
                table: "machine_field_values");

            migrationBuilder.DropIndex(
                name: "IX_field_evidence_organization_id_extraction_run_id_machine_fi~",
                table: "field_evidence");

            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_warnings_field_scope",
                table: "extraction_warnings");

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_warnings_field_scope",
                table: "extraction_warnings",
                sql: "(field_key IS NULL AND occurrence_index IS NULL) OR (field_key IS NOT NULL AND occurrence_index >= 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_field_evidence_machine_field_values_organization_id_machine~",
                table: "field_evidence",
                columns: new[] { "organization_id", "machine_field_value_id" },
                principalTable: "machine_field_values",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}

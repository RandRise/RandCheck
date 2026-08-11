using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveLegacyPageNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_pages_number",
                table: "extraction_pages");

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_pages_number",
                table: "extraction_pages",
                sql: "page_number >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_extraction_pages_number",
                table: "extraction_pages");

            migrationBuilder.AddCheckConstraint(
                name: "ck_extraction_pages_number",
                table: "extraction_pages",
                // Retain the legacy-safe policy during development rollback as well. Reinstating
                // the former cap could make a rollback fail on valid Phase 1 citations above 50.
                sql: "page_number >= 1");
        }
    }
}

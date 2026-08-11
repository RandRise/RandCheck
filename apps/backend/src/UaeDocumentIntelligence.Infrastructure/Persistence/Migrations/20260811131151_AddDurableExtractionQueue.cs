using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableExtractionQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "extraction_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    available_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_claimed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_heartbeat_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_safe_error_summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    result_outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extraction_jobs", x => x.id);
                    table.CheckConstraint("ck_extraction_jobs_attempts", "attempt_count >= 0 AND attempt_count <= 3");
                    table.CheckConstraint("ck_extraction_jobs_completion", "(status IN ('Succeeded','Failed','Cancelled') AND completed_at_utc IS NOT NULL) OR (status NOT IN ('Succeeded','Failed','Cancelled') AND completed_at_utc IS NULL)");
                    table.CheckConstraint("ck_extraction_jobs_lease_shape", "(status = 'Leased' AND lease_token IS NOT NULL AND lease_owner IS NOT NULL AND lease_expires_at_utc IS NOT NULL AND last_heartbeat_at_utc IS NOT NULL) OR (status <> 'Leased' AND lease_token IS NULL AND lease_owner IS NULL AND lease_expires_at_utc IS NULL AND last_heartbeat_at_utc IS NULL)");
                    table.CheckConstraint("ck_extraction_jobs_result", "(status = 'Succeeded' AND result_outcome IN ('Ready','Partial')) OR (status <> 'Succeeded' AND result_outcome IS NULL)");
                    table.CheckConstraint("ck_extraction_jobs_status", "status IN ('Pending','Leased','RetryScheduled','Succeeded','Failed','Cancelled')");
                    table.CheckConstraint("ck_extraction_jobs_timestamps", "updated_at_utc >= created_at_utc AND available_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "FK_extraction_jobs_extraction_runs_organization_id_document_id~",
                        columns: x => new { x.organization_id, x.document_id, x.extraction_run_id },
                        principalTable: "extraction_runs",
                        principalColumns: new[] { "organization_id", "document_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_extraction_jobs_due",
                table: "extraction_jobs",
                columns: new[] { "available_at_utc", "created_at_utc", "id" },
                filter: "status IN ('Pending','RetryScheduled')");

            migrationBuilder.CreateIndex(
                name: "ix_extraction_jobs_expired_lease",
                table: "extraction_jobs",
                column: "lease_expires_at_utc",
                filter: "status = 'Leased'");

            migrationBuilder.CreateIndex(
                name: "IX_extraction_jobs_organization_id_document_id_extraction_run_~",
                table: "extraction_jobs",
                columns: new[] { "organization_id", "document_id", "extraction_run_id" });

            migrationBuilder.CreateIndex(
                name: "ux_extraction_jobs_org_document_active",
                table: "extraction_jobs",
                columns: new[] { "organization_id", "document_id" },
                unique: true,
                filter: "status IN ('Pending','Leased','RetryScheduled')");

            migrationBuilder.CreateIndex(
                name: "ux_extraction_jobs_org_run",
                table: "extraction_jobs",
                columns: new[] { "organization_id", "extraction_run_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "extraction_jobs");
        }
    }
}

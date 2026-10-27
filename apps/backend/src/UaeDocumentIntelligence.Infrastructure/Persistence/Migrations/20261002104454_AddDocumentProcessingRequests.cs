using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_processing_requests",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    request_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character(64)", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extraction_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_processing_requests", x => new { x.organization_id, x.actor_id, x.kind, x.request_key });
                    table.CheckConstraint("ck_document_processing_requests_hash", "payload_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_document_processing_requests_key", "request_key ~ '^[A-Za-z0-9._:-]{1,100}$'");
                    table.CheckConstraint("ck_document_processing_requests_kind", "kind IN ('Upload','Reprocess')");
                    table.CheckConstraint("ck_document_processing_requests_run", "run_number > 0");
                    table.ForeignKey(
                        name: "FK_document_processing_requests_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_processing_requests_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_processing_requests");
        }
    }
}

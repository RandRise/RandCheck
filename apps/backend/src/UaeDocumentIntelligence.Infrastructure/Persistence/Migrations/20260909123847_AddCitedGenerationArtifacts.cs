using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCitedGenerationArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_cited_spend_reservations_organization_id_id_actor_id",
                table: "cited_spend_reservations",
                columns: new[] { "organization_id", "id", "actor_id" });

            migrationBuilder.CreateTable(
                name: "cited_generation_artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload_utf8 = table.Column<byte[]>(type: "bytea", nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_generation_artifacts", x => x.id);
                    table.CheckConstraint("ck_cited_generation_artifact_bytes", "octet_length(payload_utf8) BETWEEN 1 AND CASE WHEN kind = 'Preparation' THEN 98304 ELSE 524288 END");
                    table.CheckConstraint("ck_cited_generation_artifact_hashes", "payload_sha256 ~ '^[a-f0-9]{64}$' AND command_sha256 ~ '^[a-f0-9]{64}$'");
                    table.CheckConstraint("ck_cited_generation_artifact_kind", "kind IN ('Preparation', 'Outcome')");
                    table.ForeignKey(
                        name: "FK_cited_generation_artifacts_cited_spend_reservations_organiz~",
                        columns: x => new { x.organization_id, x.reservation_id, x.actor_id },
                        principalTable: "cited_spend_reservations",
                        principalColumns: new[] { "organization_id", "id", "actor_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cited_generation_artifacts_organization_id_reservation_id_a~",
                table: "cited_generation_artifacts",
                columns: new[] { "organization_id", "reservation_id", "actor_id" });

            migrationBuilder.CreateIndex(
                name: "ux_cited_generation_artifact_kind",
                table: "cited_generation_artifacts",
                columns: new[] { "organization_id", "reservation_id", "kind" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION cited_generation_artifact_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Generation artifacts are immutable.';
                END;
                $$;
                CREATE TRIGGER cited_generation_artifact_immutable
                BEFORE UPDATE OR DELETE ON cited_generation_artifacts
                FOR EACH ROW EXECUTE FUNCTION cited_generation_artifact_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cited_generation_artifacts");
            migrationBuilder.Sql("DROP FUNCTION cited_generation_artifact_immutable();");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_cited_spend_reservations_organization_id_id_actor_id",
                table: "cited_spend_reservations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCitedRecoveryAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_cited_spend_events_organization_id_id",
                table: "cited_spend_events",
                columns: new[] { "organization_id", "id" });

            migrationBuilder.CreateTable(
                name: "cited_recovery_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spend_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    command_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    before_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    after_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    before_event_sequence = table.Column<int>(type: "integer", nullable: false),
                    after_event_sequence = table.Column<int>(type: "integer", nullable: false),
                    actual_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    evidence_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evidence_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_recovery_audits", x => x.id);
                    table.CheckConstraint("ck_cited_recovery_hashes", "command_sha256 ~ '^[0-9a-f]{64}$' AND (evidence_sha256 IS NULL OR evidence_sha256 ~ '^[0-9a-f]{64}$')");
                    table.CheckConstraint("ck_cited_recovery_sequence", "before_event_sequence > 0 AND after_event_sequence = before_event_sequence + 1");
                    table.CheckConstraint("ck_cited_recovery_transition", "(action = 'CancelReserved' AND before_state = 'Reserved' AND after_state = 'Cancelled' AND reason_code = 'not_dispatched' AND actual_usd IS NULL AND evidence_id IS NULL AND evidence_reference IS NULL AND evidence_sha256 IS NULL) OR (action = 'RecordVerifiedCost' AND before_state IN ('Dispatched','Uncertain') AND after_state = 'Settled' AND reason_code = 'verified_final_cost' AND actual_usd IS NOT NULL AND actual_usd >= 0 AND evidence_id IS NOT NULL AND evidence_reference IS NOT NULL AND evidence_sha256 IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_cited_recovery_audits_actors_organization_id_operator_actor~",
                        columns: x => new { x.organization_id, x.operator_actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cited_recovery_audits_actors_organization_id_original_actor~",
                        columns: x => new { x.organization_id, x.original_actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cited_recovery_audits_cited_spend_events_organization_id_sp~",
                        columns: x => new { x.organization_id, x.spend_event_id },
                        principalTable: "cited_spend_events",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cited_recovery_audits_cited_spend_reservations_organization~",
                        columns: x => new { x.organization_id, x.reservation_id },
                        principalTable: "cited_spend_reservations",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cited_recovery_audits_organization_id_operator_actor_id",
                table: "cited_recovery_audits",
                columns: new[] { "organization_id", "operator_actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cited_recovery_audits_organization_id_original_actor_id",
                table: "cited_recovery_audits",
                columns: new[] { "organization_id", "original_actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cited_recovery_audits_organization_id_reservation_id",
                table: "cited_recovery_audits",
                columns: new[] { "organization_id", "reservation_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cited_recovery_audits_organization_id_spend_event_id",
                table: "cited_recovery_audits",
                columns: new[] { "organization_id", "spend_event_id" });

            migrationBuilder.CreateIndex(
                name: "ux_cited_recovery_evidence",
                table: "cited_recovery_audits",
                column: "evidence_id",
                unique: true,
                filter: "evidence_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cited_recovery_reference",
                table: "cited_recovery_audits",
                column: "evidence_reference",
                unique: true,
                filter: "evidence_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_cited_recovery_request",
                table: "cited_recovery_audits",
                columns: new[] { "organization_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cited_recovery_reservation",
                table: "cited_recovery_audits",
                column: "reservation_id",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION cited_recovery_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Recovery audit records are immutable.';
                END;
                $$;
                CREATE TRIGGER cited_recovery_audit_immutable
                BEFORE UPDATE OR DELETE ON cited_recovery_audits
                FOR EACH ROW EXECUTE FUNCTION cited_recovery_audit_immutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cited_recovery_audits");

            migrationBuilder.Sql("DROP FUNCTION cited_recovery_audit_immutable()");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_cited_spend_events_organization_id_id",
                table: "cited_spend_events");
        }
    }
}

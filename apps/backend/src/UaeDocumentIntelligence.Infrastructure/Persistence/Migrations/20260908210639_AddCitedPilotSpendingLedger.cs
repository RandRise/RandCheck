using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCitedPilotSpendingLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cited_pilot_budgets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    ceiling_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    settled_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    requires_reconciliation = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_pilot_budgets", x => x.id);
                    table.CheckConstraint("ck_cited_pilot_amounts", "ceiling_usd > 0 AND ceiling_usd <= 8 AND settled_usd >= 0");
                    table.CheckConstraint("ck_cited_pilot_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "cited_spend_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    budget_id = table.Column<int>(type: "integer", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rate_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rate_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    rate_valid_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    maximum_input_tokens = table.Column<int>(type: "integer", nullable: false),
                    maximum_output_tokens = table.Column<int>(type: "integer", nullable: false),
                    reserved_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    actual_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_spend_reservations", x => x.id);
                    table.UniqueConstraint("AK_cited_spend_reservations_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_cited_spend_amounts", "reserved_usd > 0 AND reserved_usd <= 8 AND (actual_usd IS NULL OR actual_usd >= 0)");
                    table.CheckConstraint("ck_cited_spend_completion", "(state = 'Settled' AND actual_usd IS NOT NULL AND completed_at_utc IS NOT NULL) OR (state = 'Cancelled' AND actual_usd IS NULL AND completed_at_utc IS NOT NULL) OR (state IN ('Reserved','Dispatched','Uncertain') AND actual_usd IS NULL AND completed_at_utc IS NULL)");
                    table.CheckConstraint("ck_cited_spend_hashes", "fingerprint ~ '^[0-9a-f]{64}$' AND request_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_cited_spend_state", "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled')");
                    table.CheckConstraint("ck_cited_spend_tokens", "maximum_input_tokens BETWEEN 1 AND 8000 AND maximum_output_tokens BETWEEN 1 AND 4000");
                    table.ForeignKey(
                        name: "FK_cited_spend_reservations_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cited_spend_reservations_cited_pilot_budgets_budget_id",
                        column: x => x.budget_id,
                        principalTable: "cited_pilot_budgets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cited_spend_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actual_usd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    evidence_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cited_spend_events", x => x.id);
                    table.CheckConstraint("ck_cited_spend_event_sequence", "sequence > 0");
                    table.CheckConstraint("ck_cited_spend_event_state", "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled')");
                    table.ForeignKey(
                        name: "FK_cited_spend_events_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cited_spend_events_cited_spend_reservations_organization_id~",
                        columns: x => new { x.organization_id, x.reservation_id },
                        principalTable: "cited_spend_reservations",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "cited_pilot_budgets",
                columns: new[] { "id", "ceiling_usd", "requires_reconciliation", "settled_usd" },
                values: new object[] { 1, 8m, false, 0m });

            migrationBuilder.CreateIndex(
                name: "IX_cited_spend_events_organization_id_actor_id",
                table: "cited_spend_events",
                columns: new[] { "organization_id", "actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cited_spend_events_organization_id_reservation_id",
                table: "cited_spend_events",
                columns: new[] { "organization_id", "reservation_id" });

            migrationBuilder.CreateIndex(
                name: "ux_cited_spend_event_sequence",
                table: "cited_spend_events",
                columns: new[] { "reservation_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cited_spend_one_pending",
                table: "cited_spend_reservations",
                column: "budget_id",
                unique: true,
                filter: "state IN ('Reserved','Dispatched','Uncertain')");

            migrationBuilder.CreateIndex(
                name: "ux_cited_spend_operation",
                table: "cited_spend_reservations",
                columns: new[] { "organization_id", "actor_id", "operation_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cited_spend_events");

            migrationBuilder.DropTable(
                name: "cited_spend_reservations");

            migrationBuilder.DropTable(
                name: "cited_pilot_budgets");
        }
    }
}

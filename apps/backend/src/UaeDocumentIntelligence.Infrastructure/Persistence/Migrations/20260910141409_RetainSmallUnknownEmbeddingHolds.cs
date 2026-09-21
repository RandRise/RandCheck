using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainSmallUnknownEmbeddingHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_retained_unknown_hold() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.state='HeldUnknown' AND (TG_OP='DELETE' OR NEW IS DISTINCT FROM OLD) THEN
                        RAISE EXCEPTION 'Retained unknown holds are immutable' USING ERRCODE='23514';
                    END IF;
                    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER retained_unknown_hold_immutable BEFORE UPDATE OR DELETE ON cited_spend_reservations
                    FOR EACH ROW EXECUTE FUNCTION guard_retained_unknown_hold();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_completion",
                table: "cited_spend_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_state",
                table: "cited_spend_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_event_state",
                table: "cited_spend_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_completion",
                table: "cited_spend_reservations",
                sql: "(state = 'Settled' AND actual_usd IS NOT NULL AND completed_at_utc IS NOT NULL) OR (state IN ('Cancelled','HeldUnknown') AND actual_usd IS NULL AND completed_at_utc IS NOT NULL) OR (state IN ('Reserved','Dispatched','Uncertain') AND actual_usd IS NULL AND completed_at_utc IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_state",
                table: "cited_spend_reservations",
                sql: "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled','HeldUnknown')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_event_state",
                table: "cited_spend_events",
                sql: "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled','HeldUnknown')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM cited_spend_reservations WHERE state='HeldUnknown') THEN
                        RAISE EXCEPTION 'Cannot downgrade retained unknown holds' USING ERRCODE='23514';
                    END IF;
                END $$;
                DROP TRIGGER retained_unknown_hold_immutable ON cited_spend_reservations;
                DROP FUNCTION guard_retained_unknown_hold();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_completion",
                table: "cited_spend_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_state",
                table: "cited_spend_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_event_state",
                table: "cited_spend_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_completion",
                table: "cited_spend_reservations",
                sql: "(state = 'Settled' AND actual_usd IS NOT NULL AND completed_at_utc IS NOT NULL) OR (state = 'Cancelled' AND actual_usd IS NULL AND completed_at_utc IS NOT NULL) OR (state IN ('Reserved','Dispatched','Uncertain') AND actual_usd IS NULL AND completed_at_utc IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_state",
                table: "cited_spend_reservations",
                sql: "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_event_state",
                table: "cited_spend_events",
                sql: "state IN ('Reserved','Dispatched','Uncertain','Settled','Cancelled')");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationCreationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "creation_payload_hash",
                table: "reconciliation_cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "creation_request_key",
                table: "reconciliation_cases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_cases_creation_request",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "created_by_actor_id", "creation_request_key" },
                unique: true,
                filter: "creation_request_key IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_cases_creation_request",
                table: "reconciliation_cases",
                sql: "(creation_request_key IS NULL AND creation_payload_hash IS NULL) OR (creation_request_key IS NOT NULL AND length(btrim(creation_request_key)) > 0 AND creation_payload_hash IS NOT NULL AND creation_payload_hash ~ '^[0-9a-f]{64}$')");
            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_reconciliation_creation_request() RETURNS trigger
                LANGUAGE plpgsql SET search_path=pg_catalog AS $$
                BEGIN
                    IF NEW.creation_request_key IS DISTINCT FROM OLD.creation_request_key
                        OR NEW.creation_payload_hash IS DISTINCT FROM OLD.creation_payload_hash
                        OR NEW.created_by_actor_id IS DISTINCT FROM OLD.created_by_actor_id THEN
                        RAISE EXCEPTION 'Case creation identity is immutable' USING ERRCODE='55000';
                    END IF;
                    RETURN NEW;
                END $$;
                REVOKE ALL ON FUNCTION public.guard_reconciliation_creation_request() FROM PUBLIC;
                CREATE TRIGGER reconciliation_creation_request_immutable BEFORE UPDATE ON public.reconciliation_cases
                FOR EACH ROW EXECUTE FUNCTION public.guard_reconciliation_creation_request();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM public.reconciliation_cases WHERE creation_request_key IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot discard durable creation request identities' USING ERRCODE='55000';
                    END IF;
                END $$;
                DROP TRIGGER reconciliation_creation_request_immutable ON public.reconciliation_cases;
                DROP FUNCTION public.guard_reconciliation_creation_request();
                """);
            migrationBuilder.DropIndex(
                name: "ux_reconciliation_cases_creation_request",
                table: "reconciliation_cases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_cases_creation_request",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "creation_payload_hash",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "creation_request_key",
                table: "reconciliation_cases");

        }
    }
}

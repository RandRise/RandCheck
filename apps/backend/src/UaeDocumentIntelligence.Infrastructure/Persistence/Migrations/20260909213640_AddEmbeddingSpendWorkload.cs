using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingSpendWorkload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_tokens",
                table: "cited_spend_reservations");

            migrationBuilder.AddColumn<string>(
                name: "workload",
                table: "cited_spend_reservations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Generation");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_tokens",
                table: "cited_spend_reservations",
                sql: "maximum_input_tokens BETWEEN 1 AND 8000 AND ((workload = 'Generation' AND maximum_output_tokens BETWEEN 1 AND 4000) OR (workload = 'Embedding' AND maximum_output_tokens = 0))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_workload",
                table: "cited_spend_reservations",
                sql: "workload IN ('Generation','Embedding')");

            migrationBuilder.Sql("""
                CREATE FUNCTION reject_cited_spend_workload_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.workload IS DISTINCT FROM OLD.workload THEN
                        RAISE EXCEPTION 'Spending workload is immutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER cited_spend_workload_immutable BEFORE UPDATE OF workload
                ON cited_spend_reservations FOR EACH ROW EXECUTE FUNCTION reject_cited_spend_workload_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A downgrade cannot represent input-only records. Never delete them or
            // invent output tokens to make a rollback succeed.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM cited_spend_reservations WHERE workload = 'Embedding') THEN
                        RAISE EXCEPTION 'Embedding spending records prevent downgrade' USING ERRCODE = '23514';
                    END IF;
                END $$;
                DROP TRIGGER cited_spend_workload_immutable ON cited_spend_reservations;
                DROP FUNCTION reject_cited_spend_workload_change();
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_tokens",
                table: "cited_spend_reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_spend_workload",
                table: "cited_spend_reservations");

            migrationBuilder.DropColumn(
                name: "workload",
                table: "cited_spend_reservations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_spend_tokens",
                table: "cited_spend_reservations",
                sql: "maximum_input_tokens BETWEEN 1 AND 8000 AND maximum_output_tokens BETWEEN 1 AND 4000");
        }
    }
}

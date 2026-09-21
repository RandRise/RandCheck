using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLexicalRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX ix_document_chunks_lexical_english ON document_chunks USING GIN (to_tsvector('english'::regconfig, content));");
            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_runs_completion",
                table: "cited_retrieval_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_runs_status",
                table: "cited_retrieval_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_runs_completion",
                table: "cited_retrieval_runs",
                sql: "(status IN ('Running','Retrieved') AND completed_at_utc IS NULL AND failure_code IS NULL AND (status <> 'Retrieved' OR retrieval_duration_ms IS NOT NULL)) OR (status = 'Completed' AND completed_at_utc IS NOT NULL AND failure_code IS NULL) OR (status = 'Failed' AND completed_at_utc IS NOT NULL AND failure_code IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_runs_status",
                table: "cited_retrieval_runs",
                sql: "status IN ('Running','Retrieved','Completed','Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Retrieval-only rows have no compatible status in the old schema.
            // Keep their hits/history and mark them failed instead of deleting them.
            migrationBuilder.Sql("DROP INDEX ix_document_chunks_lexical_english; UPDATE cited_retrieval_runs SET status='Failed', failure_code='schema_downgrade', completed_at_utc=GREATEST(created_at_utc, CURRENT_TIMESTAMP) WHERE status='Retrieved';");
            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_runs_completion",
                table: "cited_retrieval_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cited_runs_status",
                table: "cited_retrieval_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_runs_completion",
                table: "cited_retrieval_runs",
                sql: "(status = 'Running' AND completed_at_utc IS NULL AND failure_code IS NULL) OR (status = 'Completed' AND completed_at_utc IS NOT NULL AND failure_code IS NULL) OR (status = 'Failed' AND completed_at_utc IS NOT NULL AND failure_code IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cited_runs_status",
                table: "cited_retrieval_runs",
                sql: "status IN ('Running','Completed','Failed')");
        }
    }
}

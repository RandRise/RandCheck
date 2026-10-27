using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePurchaseOrderChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy rows retain null: only future inserts receive the internal transaction marker.
            migrationBuilder.AddColumn<decimal>(name: "creation_transaction_id", table: "reconciliation_runs",
                type: "numeric(20,0)", nullable: true);
            migrationBuilder.Sql("ALTER TABLE public.reconciliation_runs ALTER COLUMN creation_transaction_id SET DEFAULT (pg_catalog.pg_current_xact_id()::text::numeric);");
            migrationBuilder.AddColumn<Guid>(
                name: "evaluation_actor_id",
                table: "reconciliation_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "order_scope",
                table: "reconciliation_runs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "difference",
                table: "reconciliation_findings",
                type: "numeric(24,6)",
                precision: 24,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_kind",
                table: "reconciliation_field_snapshots",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "extracted_row_count",
                table: "reconciliation_document_snapshots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "row_coverage_status",
                table: "reconciliation_document_snapshots",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_adapter_version",
                table: "reconciliation_document_snapshots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_model_name",
                table: "reconciliation_document_snapshots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_model_version",
                table: "reconciliation_document_snapshots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_outcome",
                table: "reconciliation_document_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_provider",
                table: "reconciliation_document_snapshots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_warnings_json",
                table: "reconciliation_document_snapshots",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reconciliation_checks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurrence_index = table.Column<int>(type: "integer", nullable: true),
                    line_identity = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    left_field_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    right_field_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    left_value = table.Column<string>(type: "text", nullable: true),
                    right_value = table.Column<string>(type: "text", nullable: true),
                    signed_difference = table.Column<decimal>(type: "numeric(24,6)", precision: 24, scale: 6, nullable: true),
                    tolerance = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    basis = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_checks", x => x.id);
                    table.CheckConstraint("ck_reconciliation_checks_finding", "status = 'NoDifference' OR finding_id IS NOT NULL");
                    table.CheckConstraint("ck_reconciliation_checks_numeric", "(tolerance IS NULL OR tolerance >= 0) AND (status <> 'CouldNotCheck' OR signed_difference IS NULL)");
                    table.CheckConstraint("ck_reconciliation_checks_occurrence", "occurrence_index IS NULL OR occurrence_index >= 0");
                    table.CheckConstraint("ck_reconciliation_checks_status", "status IN ('NoDifference','DifferenceFound','CouldNotCheck')");
                    table.ForeignKey(
                        name: "FK_reconciliation_checks_reconciliation_field_snapshots_organi~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id, x.left_field_snapshot_id },
                        principalTable: "reconciliation_field_snapshots",
                        principalColumns: new[] { "organization_id", "reconciliation_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_checks_reconciliation_field_snapshots_organ~1",
                        columns: x => new { x.organization_id, x.reconciliation_run_id, x.right_field_snapshot_id },
                        principalTable: "reconciliation_field_snapshots",
                        principalColumns: new[] { "organization_id", "reconciliation_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_checks_reconciliation_findings_organization_~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id, x.finding_id },
                        principalTable: "reconciliation_findings",
                        principalColumns: new[] { "organization_id", "reconciliation_run_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_checks_reconciliation_runs_organization_id_r~",
                        columns: x => new { x.organization_id, x.reconciliation_run_id },
                        principalTable: "reconciliation_runs",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_runs_organization_id_evaluation_actor_id",
                table: "reconciliation_runs",
                columns: new[] { "organization_id", "evaluation_actor_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_runs_pair_context",
                table: "reconciliation_runs",
                sql: "(rule_set_version <> 'invoice-po-1.0' AND order_scope IS NULL AND evaluation_actor_id IS NULL) OR (rule_set_version = 'invoice-po-1.0' AND order_scope IS NOT NULL AND order_scope IN ('Unknown','FullOrder','Partial') AND evaluation_actor_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_field_snapshots_source_kind",
                table: "reconciliation_field_snapshots",
                sql: "source_kind IS NULL OR source_kind IN ('Unchecked','Confirmed','Corrected','Added','Cleared')");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_checks_organization_id_reconciliation_run_i~1",
                table: "reconciliation_checks",
                columns: new[] { "organization_id", "reconciliation_run_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_checks_organization_id_reconciliation_run_i~2",
                table: "reconciliation_checks",
                columns: new[] { "organization_id", "reconciliation_run_id", "left_field_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_checks_organization_id_reconciliation_run_i~3",
                table: "reconciliation_checks",
                columns: new[] { "organization_id", "reconciliation_run_id", "right_field_snapshot_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_checks_organization_id_reconciliation_run_id~",
                table: "reconciliation_checks",
                columns: new[] { "organization_id", "reconciliation_run_id", "finding_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_reconciliation_runs_actors_organization_id_evaluation_actor~",
                table: "reconciliation_runs",
                columns: new[] { "organization_id", "evaluation_actor_id" },
                principalTable: "actors",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION public.set_invoice_po_creation_transaction() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.rule_set_version = 'invoice-po-1.0' THEN
                    NEW.creation_transaction_id := pg_catalog.pg_current_xact_id()::text::numeric;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER invoice_po_creation_transaction BEFORE INSERT ON public.reconciliation_runs
                  FOR EACH ROW EXECUTE FUNCTION public.set_invoice_po_creation_transaction();

                CREATE FUNCTION public.guard_invoice_po_initial_insert() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_version text; parent_transaction numeric;
                BEGIN
                  SELECT rule_set_version, creation_transaction_id INTO parent_version, parent_transaction
                    FROM public.reconciliation_runs
                    WHERE organization_id = NEW.organization_id AND id = NEW.reconciliation_run_id;
                  IF parent_version IS NULL THEN
                    RAISE EXCEPTION 'A visible owning run is required for child evidence'
                      USING ERRCODE = 'foreign_key_violation';
                  END IF;
                  IF parent_version = 'invoice-po-1.0' AND parent_transaction IS DISTINCT FROM
                      pg_catalog.pg_current_xact_id()::text::numeric THEN
                    RAISE EXCEPTION 'Pair child evidence can only be inserted in the run creation transaction';
                  END IF;
                  IF TG_TABLE_NAME = 'reconciliation_checks' AND parent_version IS NOT NULL
                    AND parent_version <> 'invoice-po-1.0' THEN
                    RAISE EXCEPTION 'Explicit checks require a pair policy run';
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER invoice_po_documents_initial_insert BEFORE INSERT ON public.reconciliation_document_snapshots
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_initial_insert();
                CREATE TRIGGER invoice_po_fields_initial_insert BEFORE INSERT ON public.reconciliation_field_snapshots
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_initial_insert();
                CREATE TRIGGER invoice_po_findings_initial_insert BEFORE INSERT ON public.reconciliation_findings
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_initial_insert();
                CREATE TRIGGER invoice_po_checks_initial_insert BEFORE INSERT ON public.reconciliation_checks
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_initial_insert();

                CREATE FUNCTION public.guard_invoice_po_run() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    IF OLD.rule_set_version = 'invoice-po-1.0' THEN
                      RAISE EXCEPTION 'Pair runs are immutable';
                    END IF;
                    RETURN OLD;
                  END IF;
                  IF OLD.rule_set_version = 'invoice-po-1.0' OR NEW.rule_set_version = 'invoice-po-1.0' THEN
                    IF OLD.rule_set_version <> 'invoice-po-1.0' OR OLD.status <> 'Running' OR NEW.status <> 'Completed'
                      OR (to_jsonb(NEW) - 'status' - 'completed_at_utc') IS DISTINCT FROM
                         (to_jsonb(OLD) - 'status' - 'completed_at_utc') THEN
                      RAISE EXCEPTION 'Pair run inputs and terminal history are immutable';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER invoice_po_run_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_runs
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_run();

                CREATE FUNCTION public.guard_invoice_po_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE frozen boolean;
                BEGIN
                  IF TG_TABLE_NAME = 'reconciliation_checks' THEN frozen := true;
                  ELSIF TG_TABLE_NAME = 'reconciliation_document_snapshots' THEN
                    frozen := OLD.source_outcome IS NOT NULL;
                    IF TG_OP = 'UPDATE' THEN frozen := frozen OR NEW.source_outcome IS NOT NULL; END IF;
                  ELSIF TG_TABLE_NAME = 'reconciliation_field_snapshots' THEN
                    frozen := OLD.source_kind IS NOT NULL;
                    IF TG_OP = 'UPDATE' THEN frozen := frozen OR NEW.source_kind IS NOT NULL; END IF;
                  ELSE
                    frozen := OLD.rule_version = 'invoice-po-1.0';
                    IF TG_OP = 'UPDATE' THEN frozen := frozen OR NEW.rule_version = 'invoice-po-1.0'; END IF;
                  END IF;
                  IF frozen THEN RAISE EXCEPTION 'Pair checks, findings and source evidence are immutable'; END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER invoice_po_checks_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_checks
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_evidence();
                CREATE TRIGGER invoice_po_documents_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_document_snapshots
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_evidence();
                CREATE TRIGGER invoice_po_fields_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_field_snapshots
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_evidence();
                CREATE TRIGGER invoice_po_findings_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_findings
                  FOR EACH ROW EXECUTE FUNCTION public.guard_invoice_po_evidence();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not discard recorded coverage or narrow a persisted large difference during rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM public.reconciliation_runs WHERE rule_set_version = 'invoice-po-1.0')
                    OR EXISTS (SELECT 1 FROM public.reconciliation_findings WHERE difference > 999999999999.999999) THEN
                    RAISE EXCEPTION 'Cannot downgrade while pair history or differences beyond legacy precision exist';
                  END IF;
                END $$;
                DROP TRIGGER invoice_po_run_immutable ON public.reconciliation_runs;
                DROP TRIGGER invoice_po_creation_transaction ON public.reconciliation_runs;
                DROP TRIGGER invoice_po_documents_initial_insert ON public.reconciliation_document_snapshots;
                DROP TRIGGER invoice_po_fields_initial_insert ON public.reconciliation_field_snapshots;
                DROP TRIGGER invoice_po_findings_initial_insert ON public.reconciliation_findings;
                DROP TRIGGER invoice_po_checks_initial_insert ON public.reconciliation_checks;
                DROP TRIGGER invoice_po_checks_immutable ON public.reconciliation_checks;
                DROP TRIGGER invoice_po_documents_immutable ON public.reconciliation_document_snapshots;
                DROP TRIGGER invoice_po_fields_immutable ON public.reconciliation_field_snapshots;
                DROP TRIGGER invoice_po_findings_immutable ON public.reconciliation_findings;
                DROP FUNCTION public.guard_invoice_po_run();
                DROP FUNCTION public.set_invoice_po_creation_transaction();
                DROP FUNCTION public.guard_invoice_po_initial_insert();
                DROP FUNCTION public.guard_invoice_po_evidence();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_reconciliation_runs_actors_organization_id_evaluation_actor~",
                table: "reconciliation_runs");

            migrationBuilder.DropTable(
                name: "reconciliation_checks");

            migrationBuilder.DropIndex(
                name: "IX_reconciliation_runs_organization_id_evaluation_actor_id",
                table: "reconciliation_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_runs_pair_context",
                table: "reconciliation_runs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_field_snapshots_source_kind",
                table: "reconciliation_field_snapshots");

            migrationBuilder.DropColumn(
                name: "evaluation_actor_id",
                table: "reconciliation_runs");

            migrationBuilder.DropColumn(
                name: "order_scope",
                table: "reconciliation_runs");
            migrationBuilder.DropColumn(name: "creation_transaction_id", table: "reconciliation_runs");

            migrationBuilder.DropColumn(
                name: "source_kind",
                table: "reconciliation_field_snapshots");

            migrationBuilder.DropColumn(
                name: "extracted_row_count",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "row_coverage_status",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "source_adapter_version",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "source_model_name",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "source_model_version",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "source_outcome",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "source_provider",
                table: "reconciliation_document_snapshots");

            migrationBuilder.DropColumn(
                name: "source_warnings_json",
                table: "reconciliation_document_snapshots");

            migrationBuilder.AlterColumn<decimal>(
                name: "difference",
                table: "reconciliation_findings",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(24,6)",
                oldPrecision: 24,
                oldScale: 6,
                oldNullable: true);
        }
    }
}

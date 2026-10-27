using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFactualInvoicePairPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION public.set_invoice_po_creation_transaction() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.rule_set_version IN ('invoice-po-1.0', 'invoice-po-2.0') THEN
                    NEW.creation_transaction_id := pg_catalog.pg_current_xact_id()::text::numeric;
                  END IF;
                  RETURN NEW;
                END $$;

CREATE OR REPLACE FUNCTION public.guard_invoice_po_initial_insert() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_version text; parent_transaction numeric;
                BEGIN
                  SELECT rule_set_version, creation_transaction_id INTO parent_version, parent_transaction
                    FROM public.reconciliation_runs
                    WHERE organization_id = NEW.organization_id AND id = NEW.reconciliation_run_id;
                  IF parent_version IS NULL THEN
                    RAISE EXCEPTION 'A visible owning run is required for child evidence'
                      USING ERRCODE = 'foreign_key_violation';
                  END IF;
                  IF parent_version IN ('invoice-po-1.0', 'invoice-po-2.0') AND parent_transaction IS DISTINCT FROM
                      pg_catalog.pg_current_xact_id()::text::numeric THEN
                    RAISE EXCEPTION 'Pair child evidence can only be inserted in the run creation transaction';
                  END IF;
                  IF TG_TABLE_NAME = 'reconciliation_checks' AND parent_version IS NOT NULL
                    AND parent_version NOT IN ('invoice-po-1.0', 'invoice-po-2.0') THEN
                    RAISE EXCEPTION 'Explicit checks require a pair policy run';
                  END IF;
                  RETURN NEW;
                END $$;

CREATE OR REPLACE FUNCTION public.guard_invoice_po_run() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP = 'DELETE' THEN
                    IF OLD.rule_set_version IN ('invoice-po-1.0', 'invoice-po-2.0') THEN
                      RAISE EXCEPTION 'Pair runs are immutable';
                    END IF;
                    RETURN OLD;
                  END IF;
                  IF OLD.rule_set_version IN ('invoice-po-1.0', 'invoice-po-2.0') OR NEW.rule_set_version IN ('invoice-po-1.0', 'invoice-po-2.0') THEN
                    IF OLD.rule_set_version NOT IN ('invoice-po-1.0', 'invoice-po-2.0') OR OLD.status <> 'Running' OR NEW.status <> 'Completed'
                      OR (to_jsonb(NEW) - 'status' - 'completed_at_utc') IS DISTINCT FROM
                         (to_jsonb(OLD) - 'status' - 'completed_at_utc') THEN
                      RAISE EXCEPTION 'Pair run inputs and terminal history are immutable';
                    END IF;
                  END IF;
                  RETURN NEW;
                END $$;

CREATE OR REPLACE FUNCTION public.guard_invoice_po_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
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
                    frozen := OLD.rule_version IN ('invoice-po-1.0', 'invoice-po-2.0');
                    IF TG_OP = 'UPDATE' THEN frozen := frozen OR NEW.rule_version IN ('invoice-po-1.0', 'invoice-po-2.0'); END IF;
                  END IF;
                  IF frozen THEN RAISE EXCEPTION 'Pair checks, findings and source evidence are immutable'; END IF;
                  IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                  RETURN NEW;
                END $$;
""");
            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_runs_pair_context",
                table: "reconciliation_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_runs_pair_context",
                table: "reconciliation_runs",
                sql: "(rule_set_version NOT IN ('invoice-po-1.0', 'invoice-po-2.0') AND order_scope IS NULL AND evaluation_actor_id IS NULL) OR (rule_set_version = 'invoice-po-1.0' AND order_scope IS NOT NULL AND order_scope IN ('Unknown','FullOrder','Partial') AND evaluation_actor_id IS NOT NULL) OR (rule_set_version = 'invoice-po-2.0' AND order_scope = 'Unknown' AND order_scope IS NOT NULL AND evaluation_actor_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM public.reconciliation_runs WHERE rule_set_version = 'invoice-po-2.0') THEN
                    RAISE EXCEPTION 'Cannot remove the factual policy while its saved results exist';
                  END IF;
                END $$;
""");
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION public.set_invoice_po_creation_transaction() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW.rule_set_version = 'invoice-po-1.0' THEN
                    NEW.creation_transaction_id := pg_catalog.pg_current_xact_id()::text::numeric;
                  END IF;
                  RETURN NEW;
                END $$;

CREATE OR REPLACE FUNCTION public.guard_invoice_po_initial_insert() RETURNS trigger LANGUAGE plpgsql AS $$
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

CREATE OR REPLACE FUNCTION public.guard_invoice_po_run() RETURNS trigger LANGUAGE plpgsql AS $$
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

CREATE OR REPLACE FUNCTION public.guard_invoice_po_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
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
""");
            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_runs_pair_context",
                table: "reconciliation_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_runs_pair_context",
                table: "reconciliation_runs",
                sql: "(rule_set_version <> 'invoice-po-1.0' AND order_scope IS NULL AND evaluation_actor_id IS NULL) OR (rule_set_version = 'invoice-po-1.0' AND order_scope IS NOT NULL AND order_scope IN ('Unknown','FullOrder','Partial') AND evaluation_actor_id IS NOT NULL)");
        }
    }
}

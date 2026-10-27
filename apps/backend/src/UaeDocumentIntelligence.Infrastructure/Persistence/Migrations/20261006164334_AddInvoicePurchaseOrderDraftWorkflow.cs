using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePurchaseOrderDraftWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "draft_invoice_document_id",
                table: "reconciliation_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "draft_invoice_version",
                table: "reconciliation_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "draft_purchase_order_document_id",
                table: "reconciliation_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "draft_purchase_order_version",
                table: "reconciliation_cases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "workflow_kind",
                table: "reconciliation_cases",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.CreateTable(
                name: "reconciliation_draft_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reconciliation_case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expected_version = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_version = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_draft_requests", x => x.id);
                    table.CheckConstraint("ck_reconciliation_draft_requests_hash", "payload_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_reconciliation_draft_requests_key", "length(btrim(request_key)) > 0 AND request_key = btrim(request_key)");
                    table.CheckConstraint("ck_reconciliation_draft_requests_role", "role IN ('Invoice','PurchaseOrder')");
                    table.ForeignKey(
                        name: "FK_reconciliation_draft_requests_actors_organization_id_actor_~",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reconciliation_draft_requests_reconciliation_cases_organiza~",
                        columns: x => new { x.organization_id, x.reconciliation_case_id },
                        principalTable: "reconciliation_cases",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_cases_organization_id_draft_invoice_document~",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "draft_invoice_document_id" });

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_cases_organization_id_draft_purchase_order_d~",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "draft_purchase_order_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_cases_workflow_created",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "workflow_kind", "created_at_utc", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_cases_drafts",
                table: "reconciliation_cases",
                sql: "(workflow_kind = 'InvoicePurchaseOrder' OR (draft_invoice_document_id IS NULL AND draft_purchase_order_document_id IS NULL AND draft_invoice_version IS NULL AND draft_purchase_order_version IS NULL)) AND (draft_invoice_document_id IS NULL OR draft_purchase_order_document_id IS NULL OR draft_invoice_document_id <> draft_purchase_order_document_id) AND (draft_invoice_document_id IS NULL OR draft_invoice_version IS NOT NULL) AND (draft_purchase_order_document_id IS NULL OR draft_purchase_order_version IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reconciliation_cases_workflow",
                table: "reconciliation_cases",
                sql: "workflow_kind IN ('Legacy','InvoicePurchaseOrder')");

            migrationBuilder.CreateIndex(
                name: "IX_reconciliation_draft_requests_organization_id_reconciliatio~",
                table: "reconciliation_draft_requests",
                columns: new[] { "organization_id", "reconciliation_case_id" });

            migrationBuilder.CreateIndex(
                name: "ux_reconciliation_draft_requests_intent",
                table: "reconciliation_draft_requests",
                columns: new[] { "organization_id", "actor_id", "request_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_reconciliation_cases_documents_organization_id_draft_invoic~",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "draft_invoice_document_id" },
                principalTable: "documents",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reconciliation_cases_documents_organization_id_draft_purcha~",
                table: "reconciliation_cases",
                columns: new[] { "organization_id", "draft_purchase_order_document_id" },
                principalTable: "documents",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION public.guard_reconciliation_workflow() RETURNS trigger
                LANGUAGE plpgsql SET search_path=pg_catalog AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.workflow_kind = 'InvoicePurchaseOrder' THEN
                            RAISE EXCEPTION 'Saved invoice workflow cannot be removed' USING ERRCODE='55000';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF NEW.workflow_kind IS DISTINCT FROM OLD.workflow_kind THEN
                        RAISE EXCEPTION 'Case workflow identity is immutable' USING ERRCODE='55000';
                    END IF;
                    IF OLD.status = 'Closed' AND
                        (NEW.draft_invoice_document_id IS DISTINCT FROM OLD.draft_invoice_document_id OR
                         NEW.draft_purchase_order_document_id IS DISTINCT FROM OLD.draft_purchase_order_document_id OR
                         NEW.draft_invoice_version IS DISTINCT FROM OLD.draft_invoice_version OR
                         NEW.draft_purchase_order_version IS DISTINCT FROM OLD.draft_purchase_order_version) THEN
                        RAISE EXCEPTION 'Closed draft selections cannot change' USING ERRCODE='55000';
                    END IF;
                    IF (NEW.draft_invoice_document_id IS DISTINCT FROM OLD.draft_invoice_document_id) <>
                       (NEW.draft_invoice_version IS DISTINCT FROM OLD.draft_invoice_version) OR
                       (NEW.draft_purchase_order_document_id IS DISTINCT FROM OLD.draft_purchase_order_document_id) <>
                       (NEW.draft_purchase_order_version IS DISTINCT FROM OLD.draft_purchase_order_version) OR
                       (NEW.draft_invoice_document_id IS DISTINCT FROM OLD.draft_invoice_document_id AND NEW.draft_invoice_version IS NULL) OR
                       (NEW.draft_purchase_order_document_id IS DISTINCT FROM OLD.draft_purchase_order_document_id AND NEW.draft_purchase_order_version IS NULL) THEN
                        RAISE EXCEPTION 'Draft selection version must identify each changed selection' USING ERRCODE='55000';
                    END IF;
                    IF NEW.draft_invoice_document_id IS DISTINCT FROM OLD.draft_invoice_document_id AND EXISTS (
                        SELECT 1 FROM public.reconciliation_draft_requests r WHERE r.organization_id = OLD.organization_id
                          AND r.reconciliation_case_id = OLD.id AND r.role = 'Invoice' AND r.draft_version = NEW.draft_invoice_version) OR
                       NEW.draft_purchase_order_document_id IS DISTINCT FROM OLD.draft_purchase_order_document_id AND EXISTS (
                        SELECT 1 FROM public.reconciliation_draft_requests r WHERE r.organization_id = OLD.organization_id
                          AND r.reconciliation_case_id = OLD.id AND r.role = 'PurchaseOrder' AND r.draft_version = NEW.draft_purchase_order_version) THEN
                        RAISE EXCEPTION 'Recorded draft selection versions cannot be reused' USING ERRCODE='55000';
                    END IF;
                    RETURN NEW;
                END $$;
                REVOKE ALL ON FUNCTION public.guard_reconciliation_workflow() FROM PUBLIC;
                CREATE TRIGGER reconciliation_workflow_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_cases
                FOR EACH ROW EXECUTE FUNCTION public.guard_reconciliation_workflow();

                CREATE FUNCTION public.guard_reconciliation_draft_request() RETURNS trigger
                LANGUAGE plpgsql SET search_path=pg_catalog AS $$
                BEGIN
                    RAISE EXCEPTION 'Draft intent history is immutable' USING ERRCODE='55000';
                END $$;
                REVOKE ALL ON FUNCTION public.guard_reconciliation_draft_request() FROM PUBLIC;
                CREATE TRIGGER reconciliation_draft_request_immutable BEFORE UPDATE OR DELETE ON public.reconciliation_draft_requests
                FOR EACH ROW EXECUTE FUNCTION public.guard_reconciliation_draft_request();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS(SELECT 1 FROM public.reconciliation_cases WHERE workflow_kind = 'InvoicePurchaseOrder') OR
                       EXISTS(SELECT 1 FROM public.reconciliation_draft_requests) THEN
                        RAISE EXCEPTION 'Cannot discard saved invoice workflow or durable draft intents' USING ERRCODE='55000';
                    END IF;
                END $$;
                DROP TRIGGER reconciliation_draft_request_immutable ON public.reconciliation_draft_requests;
                DROP FUNCTION public.guard_reconciliation_draft_request();
                DROP TRIGGER reconciliation_workflow_immutable ON public.reconciliation_cases;
                DROP FUNCTION public.guard_reconciliation_workflow();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_reconciliation_cases_documents_organization_id_draft_invoic~",
                table: "reconciliation_cases");

            migrationBuilder.DropForeignKey(
                name: "FK_reconciliation_cases_documents_organization_id_draft_purcha~",
                table: "reconciliation_cases");

            migrationBuilder.DropTable(
                name: "reconciliation_draft_requests");

            migrationBuilder.DropIndex(
                name: "IX_reconciliation_cases_organization_id_draft_invoice_document~",
                table: "reconciliation_cases");

            migrationBuilder.DropIndex(
                name: "IX_reconciliation_cases_organization_id_draft_purchase_order_d~",
                table: "reconciliation_cases");

            migrationBuilder.DropIndex(
                name: "ix_reconciliation_cases_workflow_created",
                table: "reconciliation_cases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_cases_drafts",
                table: "reconciliation_cases");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reconciliation_cases_workflow",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "draft_invoice_document_id",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "draft_invoice_version",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "draft_purchase_order_document_id",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "draft_purchase_order_version",
                table: "reconciliation_cases");

            migrationBuilder.DropColumn(
                name: "workflow_kind",
                table: "reconciliation_cases");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_retention_policies",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_days = table.Column<int>(type: "integer", nullable: true),
                    quarantine_hours = table.Column<int>(type: "integer", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_retention_policies", x => x.organization_id);
                    table.CheckConstraint("ck_retention_document_days", "document_days IS NULL OR document_days BETWEEN 1 AND 36500");
                    table.CheckConstraint("ck_retention_quarantine_hours", "quarantine_hours IS NULL OR quarantine_hours BETWEEN 1 AND 87600");
                    table.ForeignKey(
                        name: "FK_organization_retention_policies_organizations_organization_~",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "retention_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_holds", x => x.id);
                    table.CheckConstraint("ck_retention_hold_reason", "reason IN ('Investigation','LegalHold','AdministrativeReview')");
                    table.CheckConstraint("ck_retention_hold_release", "released_at_utc IS NULL OR released_at_utc >= created_at_utc");
                    table.ForeignKey(
                        name: "FK_retention_holds_documents_organization_id_document_id",
                        columns: x => new { x.organization_id, x.document_id },
                        principalTable: "documents",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retention_holds_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_retention_holds_organization_id_document_id",
                table: "retention_holds",
                columns: new[] { "organization_id", "document_id" },
                filter: "released_at_utc IS NULL");
            migrationBuilder.Sql("""
                CREATE FUNCTION public.protect_retention_hold() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF OLD.released_at_utc IS NOT NULL OR NEW.released_at_utc IS NULL OR NEW.version = OLD.version OR
                       ROW(NEW.id,NEW.organization_id,NEW.document_id,NEW.reason,NEW.created_at_utc) IS DISTINCT FROM
                       ROW(OLD.id,OLD.organization_id,OLD.document_id,OLD.reason,OLD.created_at_utc) THEN
                        RAISE EXCEPTION 'Hold identity and released history are immutable' USING ERRCODE='55000';
                    END IF;
                    RETURN NEW;
                END $body$;
                CREATE TRIGGER retention_hold_release_only BEFORE UPDATE ON public.retention_holds
                    FOR EACH ROW EXECUTE FUNCTION public.protect_retention_hold();
                CREATE FUNCTION public.reject_retention_delete() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN RAISE EXCEPTION 'Retention history cannot be deleted' USING ERRCODE='55000'; END $body$;
                CREATE TRIGGER retention_hold_no_delete BEFORE DELETE OR TRUNCATE ON public.retention_holds
                    FOR EACH STATEMENT EXECUTE FUNCTION public.reject_retention_delete();
                CREATE TRIGGER retention_policy_no_delete BEFORE DELETE OR TRUNCATE ON public.organization_retention_policies
                    FOR EACH STATEMENT EXECUTE FUNCTION public.reject_retention_delete();
                REVOKE DELETE, TRUNCATE ON public.retention_holds, public.organization_retention_policies FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$ BEGIN
                    IF EXISTS (SELECT 1 FROM public.retention_holds) OR EXISTS (SELECT 1 FROM public.organization_retention_policies) THEN
                        RAISE EXCEPTION 'Refusing to erase retention policy or hold history';
                    END IF;
                END $body$;
                DROP FUNCTION public.protect_retention_hold() CASCADE;
                DROP FUNCTION public.reject_retention_delete() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "organization_retention_policies");

            migrationBuilder.DropTable(
                name: "retention_holds");
        }
    }
}

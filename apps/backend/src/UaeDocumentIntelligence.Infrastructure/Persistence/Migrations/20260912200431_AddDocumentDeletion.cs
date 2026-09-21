using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_retention_holds_documents_organization_id_document_id",
                table: "retention_holds");

            migrationBuilder.CreateTable(
                name: "document_deletions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    document_version = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_deletions", x => x.id);
                    table.UniqueConstraint("AK_document_deletions_organization_id_id", x => new { x.organization_id, x.id });
                    table.CheckConstraint("ck_document_deletion_hash", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_document_deletion_size", "size_bytes > 0 AND size_bytes <= 10485760");
                    table.CheckConstraint("ck_document_deletion_time", "completed_at_utc IS NULL OR completed_at_utc >= requested_at_utc");
                    table.ForeignKey(
                        name: "FK_document_deletions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_deletions_organization_id_document_id",
                table: "document_deletions",
                columns: new[] { "organization_id", "document_id" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION public.validate_hold_document() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF NEW.document_id IS NOT NULL THEN
                        PERFORM id FROM public.documents WHERE organization_id=NEW.organization_id AND id=NEW.document_id FOR KEY SHARE;
                        IF NOT FOUND THEN RAISE EXCEPTION 'Hold requires a document in its organization' USING ERRCODE='23503'; END IF;
                    END IF;
                    RETURN NEW;
                END $body$;
                CREATE TRIGGER retention_hold_document_scope BEFORE INSERT ON public.retention_holds
                    FOR EACH ROW EXECUTE FUNCTION public.validate_hold_document();
                CREATE FUNCTION public.protect_document_deletion() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF OLD.completed_at_utc IS NOT NULL OR NEW.completed_at_utc IS NULL OR
                       (to_jsonb(NEW)-'completed_at_utc') IS DISTINCT FROM (to_jsonb(OLD)-'completed_at_utc') THEN
                        RAISE EXCEPTION 'Deletion identity and completed history are immutable' USING ERRCODE='55000';
                    END IF;
                    RETURN NEW;
                END $body$;
                CREATE TRIGGER document_deletion_complete_only BEFORE UPDATE ON public.document_deletions
                    FOR EACH ROW EXECUTE FUNCTION public.protect_document_deletion();
                CREATE TRIGGER document_deletion_no_delete BEFORE DELETE OR TRUNCATE ON public.document_deletions
                    FOR EACH STATEMENT EXECUTE FUNCTION public.reject_retention_delete();
                CREATE FUNCTION public.require_document_deletion_record() RETURNS trigger LANGUAGE plpgsql AS $body$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public.document_deletions WHERE organization_id=OLD.organization_id AND document_id=OLD.id
                        AND document_version=OLD.concurrency_token AND storage_key=OLD.storage_key AND sha256=OLD.sha256 AND size_bytes=OLD.size_bytes)
                        OR EXISTS (SELECT 1 FROM public.retention_holds WHERE organization_id=OLD.organization_id AND released_at_utc IS NULL
                            AND (document_id IS NULL OR document_id=OLD.id)) THEN
                        RAISE EXCEPTION 'Document purge requires a matching deletion record and no active hold' USING ERRCODE='23503';
                    END IF;
                    RETURN OLD;
                END $body$;
                CREATE TRIGGER document_purge_record_required BEFORE DELETE ON public.documents
                    FOR EACH ROW EXECUTE FUNCTION public.require_document_deletion_record();
                REVOKE DELETE,TRUNCATE ON public.document_deletions FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$ BEGIN
                    IF EXISTS (SELECT 1 FROM public.document_deletions) THEN RAISE EXCEPTION 'Refusing to erase deletion history'; END IF;
                END $body$;
                DROP TRIGGER document_purge_record_required ON public.documents;
                DROP FUNCTION public.require_document_deletion_record();
                DROP TRIGGER retention_hold_document_scope ON public.retention_holds;
                DROP FUNCTION public.validate_hold_document();
                DROP TRIGGER document_deletion_complete_only ON public.document_deletions;
                DROP FUNCTION public.protect_document_deletion();
                """);
            migrationBuilder.DropTable(
                name: "document_deletions");

            migrationBuilder.AddForeignKey(
                name: "FK_retention_holds_documents_organization_id_document_id",
                table: "retention_holds",
                columns: new[] { "organization_id", "document_id" },
                principalTable: "documents",
                principalColumns: new[] { "organization_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseMembershipAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "enterprise_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enterprise_identities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    before_state = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    after_state = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "clock_timestamp()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_events", x => x.id);
                    table.CheckConstraint("ck_security_audit_actor", "actor_kind IN ('Human','System','Anonymous') AND (actor_id IS NULL OR organization_id IS NOT NULL)");
                    table.CheckConstraint("ck_security_audit_outcome", "outcome IN ('Succeeded','Denied','Failed')");
                    table.ForeignKey(
                        name: "FK_security_audit_events_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_security_audit_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    can_manage_members = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => x.id);
                    table.CheckConstraint("ck_membership_role", "role IN ('None','Viewer','Reviewer','Operator')");
                    table.ForeignKey(
                        name: "FK_organization_memberships_actors_organization_id_actor_id",
                        columns: x => new { x.organization_id, x.actor_id },
                        principalTable: "actors",
                        principalColumns: new[] { "organization_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_memberships_enterprise_identities_identity_id",
                        column: x => x.identity_id,
                        principalTable: "enterprise_identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enterprise_identities_tenant_id_object_id",
                table: "enterprise_identities",
                columns: new[] { "tenant_id", "object_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_actor_id",
                table: "organization_memberships",
                column: "actor_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_identity_id",
                table: "organization_memberships",
                column: "identity_id",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_organization_id_actor_id",
                table: "organization_memberships",
                columns: new[] { "organization_id", "actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_organization_id_identity_id",
                table: "organization_memberships",
                columns: new[] { "organization_id", "identity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_organization_id_actor_id",
                table: "security_audit_events",
                columns: new[] { "organization_id", "actor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_organization_id_recorded_at_utc_id",
                table: "security_audit_events",
                columns: new[] { "organization_id", "recorded_at_utc", "id" });
            migrationBuilder.Sql("""
                CREATE FUNCTION public.reject_security_audit_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $body$
                BEGIN
                    RAISE EXCEPTION 'Security audit events are append-only' USING ERRCODE = '55000';
                END $body$;
                CREATE TRIGGER security_audit_append_only
                    BEFORE UPDATE OR DELETE OR TRUNCATE ON public.security_audit_events
                    FOR EACH STATEMENT EXECUTE FUNCTION public.reject_security_audit_mutation();
                REVOKE UPDATE, DELETE, TRUNCATE ON public.security_audit_events FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public.security_audit_events) OR
                       EXISTS (SELECT 1 FROM public.organization_memberships) OR
                       EXISTS (SELECT 1 FROM public.enterprise_identities) THEN
                        RAISE EXCEPTION 'Refusing to erase enterprise identity or audit history';
                    END IF;
                END $body$;
                DROP TRIGGER security_audit_append_only ON public.security_audit_events;
                DROP FUNCTION public.reject_security_audit_mutation();
                """);
            migrationBuilder.DropTable(
                name: "organization_memberships");

            migrationBuilder.DropTable(
                name: "security_audit_events");

            migrationBuilder.DropTable(
                name: "enterprise_identities");
        }
    }
}

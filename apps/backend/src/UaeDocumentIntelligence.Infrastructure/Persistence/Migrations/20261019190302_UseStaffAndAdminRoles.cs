using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UaeDocumentIntelligence.Infrastructure.Persistence.Migrations;

public partial class UseStaffAndAdminRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Lock both permission inputs before checking the reviewed snapshot. EF executes
        // this guard, the audit inserts and schema change in the same transaction.
        migrationBuilder.Sql("LOCK TABLE organizations, actors, enterprise_identities, organization_memberships IN ACCESS EXCLUSIVE MODE;");
        migrationBuilder.Sql("CREATE TEMP TABLE reviewed_role_inventory ON COMMIT DROP AS " +
            "SELECT (" + RoleMigrationResources.InventorySql + ") AS snapshot;");
        migrationBuilder.Sql("""
            DO $guard$
            DECLARE expected jsonb; approved jsonb;
            BEGIN
                SELECT snapshot INTO expected FROM reviewed_role_inventory;
                IF (SELECT count(*) FROM organization_memberships) <> jsonb_array_length(expected->'members') THEN
                    RAISE EXCEPTION 'Role migration cannot omit orphaned membership records';
                END IF;
                IF EXISTS (SELECT 1 FROM organization_memberships m JOIN actors a ON a.id=m.actor_id
                    WHERE a.kind <> 'Human') THEN
                    RAISE EXCEPTION 'Service identities require separate review; human roles cannot be applied';
                END IF;
                IF EXISTS (SELECT 1 FROM organization_memberships) THEN
                    approved := NULLIF(current_setting('uaedi.approved_role_inventory', true), '')::jsonb;
                    IF approved IS DISTINCT FROM expected THEN
                        RAISE EXCEPTION 'Staff/Admin migration requires the exact reviewed membership inventory; no access changed';
                    END IF;
                END IF;
            END $guard$;

            INSERT INTO security_audit_events
                (id, organization_id, actor_id, actor_kind, action, target_type, target_id,
                 outcome, reason_code, request_id, before_state, after_state)
            SELECT gen_random_uuid(), m.organization_id, NULL, 'System', 'membership.roles-migrated',
                'membership', m.id, 'Succeeded', 'reviewed-role-inventory', 'migration:20261019190302',
                m.role || ';manage=' || CASE WHEN m.can_manage_members THEN 'True' ELSE 'False' END
                    || ';active=' || CASE WHEN m.is_active THEN 'True' ELSE 'False' END,
                CASE WHEN m.can_manage_members THEN 'Admin' ELSE 'Staff' END || ';active='
                    || CASE WHEN m.is_active AND (m.role <> 'None' OR m.can_manage_members) THEN 'True' ELSE 'False' END
            FROM organization_memberships m;
            """);
        migrationBuilder.DropCheckConstraint("ck_membership_role", "organization_memberships");
        migrationBuilder.Sql("""
            UPDATE organization_memberships SET
                is_active = is_active AND (role <> 'None' OR can_manage_members),
                role = CASE WHEN can_manage_members THEN 'Admin' ELSE 'Staff' END,
                version = gen_random_uuid(), updated_at_utc = clock_timestamp();
            """);
        migrationBuilder.DropColumn("can_manage_members", "organization_memberships");
        migrationBuilder.AddCheckConstraint("ck_membership_role", "organization_memberships", "role IN ('Staff','Admin')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Empty disposable databases may roll back. Reconstructing old roles for
        // any retained membership would lose access history and is forbidden.
        migrationBuilder.Sql("""
            LOCK TABLE organization_memberships IN ACCESS EXCLUSIVE MODE;
            DO $guard$ BEGIN
                IF EXISTS (SELECT 1 FROM organization_memberships) THEN
                    RAISE EXCEPTION 'Role downgrade with retained memberships is unsafe; use a reviewed backup or forward repair';
                END IF;
            END $guard$;
            """);
        migrationBuilder.DropCheckConstraint("ck_membership_role", "organization_memberships");
        migrationBuilder.AddColumn<bool>("can_manage_members", "organization_memberships", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddCheckConstraint("ck_membership_role", "organization_memberships", "role IN ('None','Viewer','Reviewer','Operator')");
    }
}

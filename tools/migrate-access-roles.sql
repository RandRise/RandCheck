START TRANSACTION;

LOCK TABLE organizations, actors, enterprise_identities, organization_memberships IN ACCESS EXCLUSIVE MODE;

CREATE TEMP TABLE reviewed_role_inventory ON COMMIT DROP AS SELECT (SELECT jsonb_build_object(
    'database', current_database(),
    'members', COALESCE(jsonb_agg(jsonb_build_object(
        'id', m.id, 'organizationId', m.organization_id,
        'identityId', m.identity_id, 'tenantId', i.tenant_id, 'objectId', i.object_id,
        'actorId', m.actor_id,
        'version', m.version, 'oldRole', m.role,
        'oldAdmin', m.can_manage_members, 'oldActive', m.is_active,
        'actorActive', a.is_active, 'actorKind', a.kind,
        'displayName', a.display_name,
        'newRole', CASE WHEN m.can_manage_members THEN 'Admin' ELSE 'Staff' END,
        'newActive', m.is_active AND (m.role <> 'None' OR m.can_manage_members),
        'change', CASE
            WHEN NOT m.is_active OR NOT a.is_active THEN 'Remains disabled; review new rights before reactivation'
            WHEN m.role = 'None' AND NOT m.can_manage_members THEN 'Disable membership to preserve no access'
            WHEN m.can_manage_members AND m.role <> 'Operator' THEN 'Adds full document work to administration'
            WHEN m.role = 'Viewer' THEN 'Adds uploads, review changes and comparison decisions'
            WHEN m.role = 'Reviewer' THEN 'Adds upload, reprocessing and cancellation'
            ELSE 'Same capabilities under the new role name' END
    ) ORDER BY m.id) FILTER (WHERE m.id IS NOT NULL), '[]'::jsonb))
FROM organization_memberships m
JOIN actors a ON a.id = m.actor_id AND a.organization_id = m.organization_id
JOIN enterprise_identities i ON i.id = m.identity_id) AS snapshot;

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

ALTER TABLE organization_memberships DROP CONSTRAINT ck_membership_role;

UPDATE organization_memberships SET
    is_active = is_active AND (role <> 'None' OR can_manage_members),
    role = CASE WHEN can_manage_members THEN 'Admin' ELSE 'Staff' END,
    version = gen_random_uuid(), updated_at_utc = clock_timestamp();

ALTER TABLE organization_memberships DROP COLUMN can_manage_members;

ALTER TABLE organization_memberships ADD CONSTRAINT ck_membership_role CHECK (role IN ('Staff','Admin'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261019190302_UseStaffAndAdminRoles', '8.0.31');

COMMIT;


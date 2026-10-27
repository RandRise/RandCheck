SELECT jsonb_build_object(
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
JOIN enterprise_identities i ON i.id = m.identity_id;

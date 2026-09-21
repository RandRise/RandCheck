-- Legacy security-table subset. Full API deployments use EnterpriseAdmin api-role / verify-api-role.
-- Run as deployment owner: psql ... -v runtime_role=enterprise_api -f this-file
-- The role must already exist. This script creates no login and handles no password.
\set ON_ERROR_STOP on
BEGIN;
SELECT set_config('phase6.runtime_role', :'runtime_role', true);
DO $body$
DECLARE
    target text := current_setting('phase6.runtime_role');
    target_oid oid;
    table_name text;
    column_names text;
BEGIN
    SELECT oid INTO target_oid FROM pg_roles WHERE rolname = target AND NOT rolsuper AND NOT rolcreaterole AND NOT rolbypassrls;
    IF target_oid IS NULL THEN RAISE EXCEPTION 'Runtime role must exist and have no elevated role attributes'; END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE (rolsuper OR rolcreaterole OR rolbypassrls) AND pg_has_role(target_oid, oid, 'MEMBER')) THEN
        RAISE EXCEPTION 'Runtime role must not inherit or switch to elevated roles';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_class WHERE relnamespace='public'::regnamespace AND pg_has_role(target_oid, relowner, 'MEMBER')) OR
       EXISTS (SELECT 1 FROM pg_namespace WHERE nspname='public' AND pg_has_role(target_oid, nspowner, 'MEMBER')) OR
       EXISTS (SELECT 1 FROM pg_database WHERE datname=current_database() AND pg_has_role(target_oid, datdba, 'MEMBER')) THEN
        RAISE EXCEPTION 'Runtime role must not own the database, schema or tables, directly or through membership';
    END IF;
    EXECUTE format('REVOKE CREATE ON SCHEMA public FROM %I', target);
    IF has_schema_privilege(target, 'public', 'CREATE') THEN RAISE EXCEPTION 'Remove inherited/public schema CREATE privilege first'; END IF;
    FOREACH table_name IN ARRAY ARRAY['security_audit_events','enterprise_identities','organization_memberships','organization_retention_policies','retention_holds','document_deletions'] LOOP
        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM %I', table_name, target);
        SELECT string_agg(quote_ident(attname), ',') INTO column_names FROM pg_attribute
            WHERE attrelid=('public.' || table_name)::regclass AND attnum>0 AND NOT attisdropped;
        EXECUTE format('REVOKE UPDATE (%s) ON public.%I FROM %I', column_names, table_name, target);
        IF has_table_privilege(target, 'public.' || table_name, 'UPDATE,DELETE,TRUNCATE,TRIGGER') OR
           EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid=('public.' || table_name)::regclass AND a.attnum>0 AND
                has_column_privilege(target, a.attrelid, a.attnum, 'UPDATE')) THEN
            RAISE EXCEPTION 'Remove inherited/public or column-level mutation privileges first';
        END IF;
        EXECUTE format('GRANT SELECT, INSERT ON TABLE public.%I TO %I', table_name, target);
    END LOOP;
    EXECUTE format('GRANT UPDATE (role,can_manage_members,is_active,version,updated_at_utc) ON public.organization_memberships TO %I', target);
    EXECUTE format('GRANT UPDATE (document_days,quarantine_hours,version,updated_at_utc) ON public.organization_retention_policies TO %I', target);
    EXECUTE format('GRANT UPDATE (released_at_utc,version) ON public.retention_holds TO %I', target);
    EXECUTE format('GRANT UPDATE (completed_at_utc) ON public.document_deletions TO %I', target);
END $body$;
COMMIT;

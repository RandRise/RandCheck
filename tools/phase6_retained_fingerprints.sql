-- Read-only, full-row fingerprints; never reset, migrate or rewrite retained data.
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ READ ONLY;
SELECT format('SELECT %L, count(*), md5(coalesce(string_agg(row_to_json(s)::text, '''' ORDER BY row_to_json(s)::text), '''')) FROM (SELECT * FROM %I.%I) s;',
    tablename, schemaname, tablename)
FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename
\gexec
COMMIT;

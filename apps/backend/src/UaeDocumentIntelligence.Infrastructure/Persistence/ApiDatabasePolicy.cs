using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

// One allowlist drives provisioning and verification; no credentials or DDL at runtime.
public static class ApiDatabasePolicy
{
    private static readonly HashSet<string> ReadTables = ["organizations", "actors", "enterprise_identities", "organization_memberships",
        "organization_capacity_policies", "organization_retention_policies", "retention_holds", "document_deletions", "security_audit_events", "documents", "document_processing_requests",
        "extraction_jobs", "extraction_runs", "extraction_pages", "machine_field_values", "field_evidence", "extraction_warnings",
        "review_sessions", "review_actions", "reconciliation_cases", "reconciliation_draft_requests", "reconciliation_document_links", "reconciliation_runs",
        "reconciliation_document_snapshots", "reconciliation_field_snapshots", "reconciliation_findings", "reconciliation_checks", "finding_resolutions",
        "cited_document_indexes", "document_chunks", "chunk_evidence_spans", "cited_retrieval_runs", "retrieval_sources", "retrieval_hits",
        "cited_answers", "answer_claims", "answer_citations", "cited_chunk_embeddings", "cited_pilot_budgets",
        "cited_spend_reservations", "cited_generation_artifacts"];
    private static readonly HashSet<string> InsertTables = ["actors", "enterprise_identities", "organization_memberships",
        "organization_retention_policies", "retention_holds", "document_deletions", "security_audit_events", "documents", "document_processing_requests",
        "extraction_jobs", "extraction_runs", "review_sessions", "review_actions", "reconciliation_cases", "reconciliation_draft_requests", "reconciliation_document_links",
        "reconciliation_runs", "reconciliation_document_snapshots", "reconciliation_field_snapshots", "reconciliation_findings", "reconciliation_checks",
        "finding_resolutions", "cited_document_indexes", "document_chunks", "chunk_evidence_spans", "cited_retrieval_runs",
        "retrieval_sources", "retrieval_hits", "cited_answers", "answer_claims", "answer_citations"];
    private static readonly HashSet<string> DeleteTables = ["documents", "extraction_jobs", "extraction_runs", "reconciliation_document_links"];
    private static readonly Dictionary<string, string[]> ReadColumns = new();
    private static readonly Dictionary<string, string[]> UpdateColumns = new()
    {
        // PostgreSQL row locks require UPDATE on one column. These two descriptive
        // fields are permitted for locking; identity and active status remain protected.
        ["organizations"] = ["display_name"], ["actors"] = ["display_name"],
        // The singleton CHECK(id=1) permits locking without changing any budget values.
        ["cited_pilot_budgets"] = ["id"],
        ["organization_memberships"] = ["role", "is_active", "version", "updated_at_utc"],
        ["organization_retention_policies"] = ["document_days", "quarantine_hours", "version", "updated_at_utc"],
        ["retention_holds"] = ["released_at_utc", "version"], ["document_deletions"] = ["completed_at_utc"],
        ["documents"] = ["processing_status", "updated_at_utc", "concurrency_token"],
        ["extraction_jobs"] = ["status", "lease_token", "lease_owner", "lease_expires_at_utc", "last_heartbeat_at_utc",
            "last_error_code", "last_safe_error_summary", "result_outcome", "updated_at_utc", "completed_at_utc"],
        ["extraction_runs"] = ["status", "completed_at_utc", "duration_ms", "failure_code", "safe_failure_summary"],
        ["review_sessions"] = ["status", "assigned_actor_id", "started_at_utc", "completed_at_utc", "concurrency_token"],
        ["reconciliation_cases"] = ["name", "category", "status", "updated_at_utc", "closed_at_utc", "concurrency_token",
            "draft_invoice_document_id", "draft_purchase_order_document_id", "draft_invoice_version", "draft_purchase_order_version"],
        ["reconciliation_runs"] = ["status", "completed_at_utc"],
        ["cited_document_indexes"] = ["status", "concurrency_token"],
        ["cited_retrieval_runs"] = ["status", "concurrency_token", "completed_at_utc", "retrieval_duration_ms", "failure_code"]
    };
    public static string Subject(string role)
    {
        if (role is null || !Regex.IsMatch(role, "\\A[a-z][a-z0-9_]{0,62}\\z"))
            throw new InvalidOperationException("A valid dedicated API database role is required.");
        return "service:enterprise-api:" + role;
    }

    private static bool ColumnAllowed(Dictionary<string, string[]> rules, string table, string column) =>
        rules.TryGetValue(table, out var columns) && columns.Contains(column);

    public static async Task ConfigureAsync(AppDbContext db, string role, CancellationToken ct)
    {
        _ = Subject(role);
        await db.Database.OpenConnectionAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await VerifyRoleAsync(db, role, false, ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var names = new List<(string Table, string Column)>();
        await using (var command = new NpgsqlCommand("SELECT c.relname,a.attname FROM pg_class c JOIN pg_attribute a ON a.attrelid=c.oid WHERE c.relnamespace='public'::regnamespace AND c.relkind IN ('r','p','v','m','f') AND a.attnum>0 AND NOT a.attisdropped", connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) names.Add((reader.GetString(0), reader.GetString(1)));
        static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var sql = $"GRANT USAGE ON SCHEMA public TO {Q(role)}; REVOKE CREATE ON SCHEMA public FROM {Q(role)}; REVOKE ALL ON ALL TABLES IN SCHEMA public FROM {Q(role)};";
        foreach (var table in names.Select(n => n.Table).Distinct())
        {
            var columns = string.Join(',', names.Where(n => n.Table == table).Select(n => Q(n.Column)));
            sql += $"REVOKE ALL ON public.{Q(table)} FROM {Q(role)}; REVOKE SELECT ({columns}), INSERT ({columns}), UPDATE ({columns}), REFERENCES ({columns}) ON public.{Q(table)} FROM {Q(role)};";
            if (ReadTables.Contains(table)) sql += $"GRANT SELECT ON public.{Q(table)} TO {Q(role)};";
            if (DeleteTables.Contains(table)) sql += $"GRANT DELETE ON public.{Q(table)} TO {Q(role)};";
            if (InsertTables.Contains(table)) sql += $"GRANT INSERT ON public.{Q(table)} TO {Q(role)};";
            if (ReadColumns.TryGetValue(table, out var read)) sql += $"GRANT SELECT ({string.Join(',', read.Select(Q))}) ON public.{Q(table)} TO {Q(role)};";
            if (UpdateColumns.TryGetValue(table, out var update)) sql += $"GRANT UPDATE ({string.Join(',', update.Select(Q))}) ON public.{Q(table)} TO {Q(role)};";
        }
        await using (var command = new NpgsqlCommand(sql, connection)) await command.ExecuteNonQueryAsync(ct);
        await VerifyAsync(db, role, false, ct);
        db.SecurityAuditEvents.Add(new UaeDocumentIntelligence.Domain.Model.SecurityAuditEvent(null, null, "System",
            "api.database-role-configured", "database-role", null, "Succeeded", "committed", Guid.NewGuid().ToString("N"),
            afterState: $"role={role};policy=enterprise-api-v1"));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task VerifyRoleAsync(AppDbContext db, string role, bool current, CancellationToken ct)
    {
        _ = Subject(role);
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname=@role
              AND NOT (r.rolsuper OR r.rolcreaterole OR r.rolcreatedb OR r.rolreplication OR r.rolbypassrls)
              AND NOT EXISTS (SELECT 1 FROM pg_auth_members WHERE member=r.oid)
              AND NOT EXISTS (SELECT 1 FROM pg_database WHERE datdba=r.oid)
              AND NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspowner=r.oid)
              AND NOT EXISTS (SELECT 1 FROM pg_class WHERE relowner=r.oid)
              AND NOT has_database_privilege(r.oid,current_database(),'CREATE')
              AND NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                WHERE n.nspname !~ '^pg_' AND n.nspname<>'information_schema' AND
                ((c.relkind='S' AND has_sequence_privilege(r.oid,c.oid,'USAGE,SELECT,UPDATE')) OR
                 (n.nspname<>'public' AND c.relkind IN ('r','p','v','m','f') AND
                   (has_table_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER') OR
                    has_any_column_privilege(r.oid,c.oid,'SELECT,INSERT,UPDATE,REFERENCES')))))
              AND NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname !~ '^pg_' AND nspname<>'information_schema' AND has_schema_privilege(r.oid,oid,'CREATE'))
              AND NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE p.prosecdef AND n.nspname !~ '^pg_' AND n.nspname<>'information_schema' AND has_function_privilege(r.oid,p.oid,'EXECUTE')))
              AND (NOT @current OR current_user=@role)
            """, (NpgsqlConnection)db.Database.GetDbConnection());
        command.Parameters.AddWithValue("role", role); command.Parameters.AddWithValue("current", current);
        if (await command.ExecuteScalarAsync(ct) is not true)
            throw new InvalidOperationException("The API database role does not satisfy the restricted-role policy.");
    }

    public static async Task VerifyAsync(AppDbContext db, string role, bool requireCurrentRole, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await VerifyRoleAsync(db, role, requireCurrentRole, ct);
        // Include relations without columns; the column-level scan below cannot see them.
        await using (var relations = new NpgsqlCommand("""
            SELECT has_schema_privilege(@role,'public','USAGE') AND NOT EXISTS (
              SELECT 1 FROM pg_class c WHERE c.relnamespace='public'::regnamespace
                AND c.relkind IN ('r','p','v','m','f') AND c.relname<>ALL(@tables)
                AND (has_table_privilege(@role,c.oid,'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
                  OR has_any_column_privilege(@role,c.oid,'SELECT,INSERT,UPDATE,REFERENCES')))
            """, (NpgsqlConnection)db.Database.GetDbConnection()))
        {
            relations.Parameters.AddWithValue("role", role);
            relations.Parameters.AddWithValue("tables", ReadTables.Concat(InsertTables).Concat(DeleteTables).Concat(UpdateColumns.Keys).Distinct().ToArray());
            if (await relations.ExecuteScalarAsync(ct) is not true)
                throw new InvalidOperationException("API database schema grants differ from the API allowlist.");
        }
        await using var command = new NpgsqlCommand("""
            SELECT c.relname, a.attname,
              has_column_privilege(@role,c.oid,a.attnum,'SELECT'),
              has_column_privilege(@role,c.oid,a.attnum,'INSERT'),
              has_column_privilege(@role,c.oid,a.attnum,'UPDATE'),
              has_column_privilege(@role,c.oid,a.attnum,'REFERENCES'),
              has_table_privilege(@role,c.oid,'TRUNCATE,TRIGGER,DELETE WITH GRANT OPTION'),
              has_column_privilege(@role,c.oid,a.attnum,'SELECT WITH GRANT OPTION,INSERT WITH GRANT OPTION,UPDATE WITH GRANT OPTION,REFERENCES WITH GRANT OPTION'),
              has_table_privilege(@role,c.oid,'DELETE')
            FROM pg_class c JOIN pg_attribute a ON a.attrelid=c.oid
            WHERE c.relnamespace='public'::regnamespace AND c.relkind IN ('r','p','v','m','f')
              AND a.attnum>0 AND NOT a.attisdropped
            """, (NpgsqlConnection)db.Database.GetDbConnection());
        command.Parameters.AddWithValue("role", role);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var seen = new HashSet<string>();
        var columnsSeen = new HashSet<(string, string)>();
        while (await reader.ReadAsync(ct))
        {
            var table = reader.GetString(0); var column = reader.GetString(1); seen.Add(table);
            columnsSeen.Add((table, column));
            if (reader.GetBoolean(2) != (ReadTables.Contains(table) || ColumnAllowed(ReadColumns, table, column)) ||
                reader.GetBoolean(3) != InsertTables.Contains(table) ||
                reader.GetBoolean(4) != ColumnAllowed(UpdateColumns, table, column) || reader.GetBoolean(5) || reader.GetBoolean(6) || reader.GetBoolean(7) || reader.GetBoolean(8) != DeleteTables.Contains(table))
                throw new InvalidOperationException("API database grants differ from the API allowlist.");
        }
        if (!ReadTables.Concat(InsertTables).Concat(ReadColumns.Keys).Concat(UpdateColumns.Keys).All(seen.Contains))
            throw new InvalidOperationException("The API database schema is not provisioned.");
        if (UpdateColumns.Any(r => r.Value.Any(column => !columnsSeen.Contains((r.Key, column)))))
            throw new InvalidOperationException("Required API database columns are missing.");
    }
}

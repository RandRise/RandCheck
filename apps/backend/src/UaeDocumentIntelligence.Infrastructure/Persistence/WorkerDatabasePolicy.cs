using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

// One allowlist drives provisioning and verification; no credentials or DDL at runtime.
public static class WorkerDatabasePolicy
{
    private static readonly HashSet<string> ReadTables = ["documents", "extraction_jobs", "extraction_runs",
        "extraction_pages", "machine_field_values", "field_evidence", "extraction_warnings"];
    private static readonly HashSet<string> InsertTables = ["extraction_pages", "machine_field_values",
        "field_evidence", "extraction_warnings", "security_audit_events"];
    private static readonly Dictionary<string, string[]> ReadColumns = new()
    {
        ["actors"] = ["id", "organization_id", "kind", "external_subject", "is_active"],
        ["security_audit_events"] = ["recorded_at_utc"]
    };
    private static readonly Dictionary<string, string[]> UpdateColumns = new()
    {
        ["documents"] = ["processing_status", "updated_at_utc", "concurrency_token"],
        ["extraction_jobs"] = ["status", "attempt_count", "available_at_utc", "last_claimed_at_utc", "lease_token",
            "lease_owner", "lease_expires_at_utc", "last_heartbeat_at_utc", "last_error_code", "last_safe_error_summary",
            "result_outcome", "updated_at_utc", "completed_at_utc"],
        ["extraction_runs"] = ["status", "document_type", "language", "model_name", "model_version", "started_at_utc",
            "completed_at_utc", "duration_ms", "provider_duration_ms", "page_count", "estimated_cost", "cost_currency",
            "price_basis", "failure_code", "safe_failure_summary"]
    };

    public static string Subject(string role)
    {
        if (role is null || !Regex.IsMatch(role, "\\A[a-z][a-z0-9_]{0,62}\\z"))
            throw new InvalidOperationException("A valid dedicated worker database role is required.");
        return "service:extraction-worker:" + role;
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
        var sql = $"GRANT USAGE ON SCHEMA public TO {Q(role)}; REVOKE CREATE ON SCHEMA public FROM {Q(role)};";
        foreach (var table in names.Select(n => n.Table).Distinct())
        {
            var columns = string.Join(',', names.Where(n => n.Table == table).Select(n => Q(n.Column)));
            sql += $"REVOKE ALL ON public.{Q(table)} FROM {Q(role)}; REVOKE SELECT ({columns}), INSERT ({columns}), UPDATE ({columns}), REFERENCES ({columns}) ON public.{Q(table)} FROM {Q(role)};";
            if (ReadTables.Contains(table)) sql += $"GRANT SELECT ON public.{Q(table)} TO {Q(role)};";
            if (InsertTables.Contains(table)) sql += $"GRANT INSERT ON public.{Q(table)} TO {Q(role)};";
            if (ReadColumns.TryGetValue(table, out var read)) sql += $"GRANT SELECT ({string.Join(',', read.Select(Q))}) ON public.{Q(table)} TO {Q(role)};";
            if (UpdateColumns.TryGetValue(table, out var update)) sql += $"GRANT UPDATE ({string.Join(',', update.Select(Q))}) ON public.{Q(table)} TO {Q(role)};";
        }
        await using (var command = new NpgsqlCommand(sql, connection)) await command.ExecuteNonQueryAsync(ct);
        await VerifyAsync(db, role, false, ct);
        db.SecurityAuditEvents.Add(new UaeDocumentIntelligence.Domain.Model.SecurityAuditEvent(null, null, "System",
            "worker.database-role-configured", "database-role", null, "Succeeded", "committed", Guid.NewGuid().ToString("N"),
            afterState: $"role={role};policy=extraction-v1"));
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
              AND NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname !~ '^pg_' AND nspname<>'information_schema' AND has_schema_privilege(r.oid,oid,'CREATE'))
              AND NOT EXISTS (SELECT 1 FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE p.prosecdef AND n.nspname !~ '^pg_' AND n.nspname<>'information_schema' AND has_function_privilege(r.oid,p.oid,'EXECUTE')))
              AND (NOT @current OR current_user=@role)
            """, (NpgsqlConnection)db.Database.GetDbConnection());
        command.Parameters.AddWithValue("role", role); command.Parameters.AddWithValue("current", current);
        if (await command.ExecuteScalarAsync(ct) is not true)
            throw new InvalidOperationException("The worker database role does not satisfy the restricted-role policy.");
    }

    public static async Task VerifyAsync(AppDbContext db, string role, bool requireCurrentRole, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        await VerifyRoleAsync(db, role, requireCurrentRole, ct);
        await using var command = new NpgsqlCommand("""
            SELECT c.relname, a.attname,
              has_column_privilege(@role,c.oid,a.attnum,'SELECT'),
              has_column_privilege(@role,c.oid,a.attnum,'INSERT'),
              has_column_privilege(@role,c.oid,a.attnum,'UPDATE'),
              has_column_privilege(@role,c.oid,a.attnum,'REFERENCES'),
              has_table_privilege(@role,c.oid,'DELETE,TRUNCATE,TRIGGER'),
              has_column_privilege(@role,c.oid,a.attnum,'SELECT WITH GRANT OPTION,INSERT WITH GRANT OPTION,UPDATE WITH GRANT OPTION,REFERENCES WITH GRANT OPTION')
            FROM pg_class c JOIN pg_attribute a ON a.attrelid=c.oid
            WHERE c.relnamespace='public'::regnamespace AND c.relkind IN ('r','p','v','m','f')
              AND a.attnum>0 AND NOT a.attisdropped
            """, (NpgsqlConnection)db.Database.GetDbConnection());
        command.Parameters.AddWithValue("role", role);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var seen = new HashSet<string>();
        while (await reader.ReadAsync(ct))
        {
            var table = reader.GetString(0); var column = reader.GetString(1); seen.Add(table);
            if (reader.GetBoolean(2) != (ReadTables.Contains(table) || ColumnAllowed(ReadColumns, table, column)) ||
                reader.GetBoolean(3) != InsertTables.Contains(table) ||
                reader.GetBoolean(4) != ColumnAllowed(UpdateColumns, table, column) || reader.GetBoolean(5) || reader.GetBoolean(6) || reader.GetBoolean(7))
                throw new InvalidOperationException("Worker database grants differ from the extraction allowlist.");
        }
        if (!ReadTables.Concat(InsertTables).Concat(ReadColumns.Keys).All(seen.Contains))
            throw new InvalidOperationException("The worker database schema is not provisioned.");
    }
}

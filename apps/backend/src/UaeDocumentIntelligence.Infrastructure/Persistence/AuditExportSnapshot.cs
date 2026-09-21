using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed record AuditExportRequest(string ExpectedDatabase, Guid OrganizationId,
    DateTimeOffset FromUtc, DateTimeOffset BeforeUtc, int MaxRows = 10000)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ExpectedDatabase) || ExpectedDatabase.Length > 63 ||
            ExpectedDatabase.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase) || OrganizationId == Guid.Empty ||
            FromUtc.Offset != TimeSpan.Zero || BeforeUtc.Offset != TimeSpan.Zero || BeforeUtc <= FromUtc ||
            FromUtc.Ticks % 10 != 0 || BeforeUtc.Ticks % 10 != 0 || MaxRows is < 1 or > AuditArchive.MaxRows)
            throw new InvalidOperationException("Invalid audit export scope.");
    }
}

public sealed record AuditExportScope(int SchemaVersion, Guid ExportId, string Database, string DatabaseRole,
    Guid OrganizationId, DateTimeOffset FromUtc, DateTimeOffset BeforeUtc, DateTimeOffset CapturedAtUtc, int MaxRows);

public sealed record AuditExportRow(Guid Id, Guid OrganizationId, Guid? ActorId, string ActorKind, string Action,
    string TargetType, Guid? TargetId, string Outcome, string ReasonCode, string RequestId,
    string? BeforeState, string? AfterState, DateTimeOffset RecordedAtUtc);

// A private database-operator capability, never registered in the HTTP application.
// The snapshot owns a read-only Repeatable Read transaction until it is disposed.
public sealed class AuditExportSnapshot : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _consumed;
    public AuditExportScope Scope { get; }

    private AuditExportSnapshot(NpgsqlConnection connection, NpgsqlTransaction transaction, AuditExportScope scope)
    { _connection = connection; _transaction = transaction; Scope = scope; }

    public static async Task<AuditExportSnapshot> OpenAsync(string connectionString, AuditExportRequest request, CancellationToken ct)
    {
        request.Validate();
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Database != request.ExpectedDatabase || settings.IncludeErrorDetail || settings.LogParameters)
            throw new InvalidOperationException("Audit export database configuration does not match.");
        settings.Pooling = false; settings.Timeout = 10; settings.CommandTimeout = 30;
        var connection = new NpgsqlConnection(settings.ConnectionString);
        NpgsqlTransaction? transaction = null;
        try
        {
            await connection.OpenAsync(ct);
            transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            // Do not silently produce a partial archive when an RLS policy filters rows.
            await using (var mode = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL row_security=off", connection, transaction))
                await mode.ExecuteNonQueryAsync(ct);
            await using var metadata = new NpgsqlCommand("""
                SELECT current_database(), current_user, clock_timestamp(),
                    EXISTS (SELECT 1 FROM public.organizations WHERE id=@organization)
                """, connection, transaction);
            metadata.Parameters.AddWithValue("organization", request.OrganizationId);
            await using var reader = await metadata.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || !reader.GetBoolean(3) || reader.GetString(0) != request.ExpectedDatabase)
                throw new InvalidOperationException("Audit export organization is unavailable.");
            var captured = reader.GetFieldValue<DateTimeOffset>(2);
            if (request.BeforeUtc > captured) throw new InvalidOperationException("Audit export cannot cover a future interval.");
            return new(connection, transaction, new(1, Guid.NewGuid(), reader.GetString(0), reader.GetString(1),
                request.OrganizationId, request.FromUtc, request.BeforeUtc, captured, request.MaxRows));
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public async IAsyncEnumerable<AuditExportRow> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_consumed) throw new InvalidOperationException("Audit snapshot has already been read.");
        _consumed = true;
        await using var command = new NpgsqlCommand("""
            SELECT id, organization_id, actor_id, actor_kind, action, target_type, target_id,
                outcome, reason_code, request_id, before_state, after_state, recorded_at_utc
            FROM public.security_audit_events
            WHERE organization_id=@organization AND recorded_at_utc>=@from AND recorded_at_utc<@before
            ORDER BY recorded_at_utc, id LIMIT @limit
            """, _connection, _transaction);
        command.Parameters.AddWithValue("organization", Scope.OrganizationId);
        command.Parameters.AddWithValue("from", Scope.FromUtc);
        command.Parameters.AddWithValue("before", Scope.BeforeUtc);
        command.Parameters.AddWithValue("limit", Scope.MaxRows + 1);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        while (await reader.ReadAsync(ct))
            yield return new(reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11), reader.GetFieldValue<DateTimeOffset>(12));
    }

    public async ValueTask DisposeAsync()
    {
        try { await _transaction.DisposeAsync(); }
        finally { await _connection.DisposeAsync(); }
    }
}

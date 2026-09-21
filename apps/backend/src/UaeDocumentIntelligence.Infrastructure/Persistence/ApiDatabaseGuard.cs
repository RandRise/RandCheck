using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

public sealed class ApiDatabaseOptions
{
    // Explicit deployment gate; this does not change standalone/development tools.
    public bool Enabled { get; set; }
    public string DatabaseRole { get; set; } = "";
}

public sealed class ApiDatabaseGuard(AppDbContext db, IOptions<ApiDatabaseOptions> options)
{
    public async Task VerifyAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        try
        {
            _ = ApiDatabasePolicy.Subject(options.Value.DatabaseRole);
            if (!db.Database.IsNpgsql()) throw new InvalidOperationException();
            var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            var loopback = connection.Host == "localhost" || IPAddress.TryParse(connection.Host, out var address) && IPAddress.IsLoopback(address);
            if (string.IsNullOrWhiteSpace(connection.Database) || connection.Database.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase) ||
                connection.Username != options.Value.DatabaseRole || connection.IncludeErrorDetail || connection.LogParameters ||
                !loopback && connection.SslMode != SslMode.VerifyFull) throw new InvalidOperationException();
            await ApiDatabasePolicy.VerifyAsync(db, options.Value.DatabaseRole, true, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { throw new InvalidOperationException("The API database role, connection or grants are not configured."); }
    }
}

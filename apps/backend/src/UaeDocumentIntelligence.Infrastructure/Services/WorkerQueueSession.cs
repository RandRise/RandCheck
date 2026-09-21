using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Net;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class WorkerQueueSession(AppDbContext db, IExtractionJobQueue queue, bool ownsDb) : IAsyncDisposable
{
    internal AppDbContext Database => db;
    internal IExtractionJobQueue Queue => queue;
    public ValueTask DisposeAsync() => ownsDb ? db.DisposeAsync() : ValueTask.CompletedTask;

    internal static async Task<WorkerQueueSession> OpenAsync(IServiceProvider services, CancellationToken ct)
    {
        if (services.GetService<IOptions<DocumentDeletionOptions>>()?.Value.Enabled == true)
            await services.GetRequiredService<DocumentRecoveryGuard>().VerifyAsync(null, false, ct);
        var options = services.GetRequiredService<IOptions<ExtractionWorkerOptions>>().Value;
        if (options.SecurityMode == "Development")
            return new(services.GetRequiredService<AppDbContext>(), services.GetRequiredService<IExtractionJobQueue>(), false);
        if (options.SecurityMode != "Enforced") throw new InvalidOperationException("The extraction worker security profile is locked.");
        AppDbContext? database = null;
        try
        {
            var subject = WorkerDatabasePolicy.Subject(options.DatabaseRole);
            var configuration = services.GetRequiredService<IConfiguration>();
            var connection = configuration.GetConnectionString("WorkerDatabase");
            if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException();
            var parsed = new NpgsqlConnectionStringBuilder(connection);
            var api = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Database"));
            var loopback = parsed.Host == "localhost" || IPAddress.TryParse(parsed.Host, out var ip) && IPAddress.IsLoopback(ip);
            if (string.IsNullOrWhiteSpace(parsed.Database) || parsed.Database.StartsWith("phase5_", StringComparison.OrdinalIgnoreCase) ||
                parsed.Host != api.Host || parsed.Port != api.Port || parsed.Database != api.Database ||
                (!loopback && parsed.SslMode != SslMode.VerifyFull) ||
                parsed.Username != options.DatabaseRole || parsed.IncludeErrorDetail || parsed.LogParameters)
                throw new InvalidOperationException();
            database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection)
                .UseLoggerFactory(services.GetRequiredService<DatabasePrivacyLoggerFactory>()).Options);
            await WorkerDatabasePolicy.VerifyAsync(database, options.DatabaseRole, true, ct);
            return new(database, new ExtractionJobQueue(database, services.GetRequiredService<IClock>(), subject), true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (database is not null) await database.DisposeAsync();
            throw;
        }
        catch
        {
            if (database is not null) await database.DisposeAsync();
            throw new InvalidOperationException("The dedicated worker database connection, schema or permissions are not configured.");
        }
    }
}

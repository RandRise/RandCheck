using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class StorageMaintenanceOptions
{
    public bool Enabled { get; set; }
    public int OrphanGraceMinutes { get; set; } = 60;
    public int CleanupIntervalMinutes { get; set; } = 30;
}

internal sealed class StorageMaintenanceService(
    IServiceScopeFactory scopes,
    IOptions<StorageOptions> storageOptions,
    IOptions<StorageMaintenanceOptions> maintenanceOptions,
    ILogger<StorageMaintenanceService> logger) : BackgroundService
{
    private static readonly Regex ManagedKeyPattern = new(
        "^organizations/[0-9a-f]{32}/documents/[0-9a-f]{32}/source\\.[a-z0-9]{1,10}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!maintenanceOptions.Value.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await CleanupAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} unreferenced upload artifacts beyond the grace window",
                        deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Upload orphan cleanup failed safely and will retry later");
            }
            await Task.Delay(TimeSpan.FromMinutes(
                Math.Max(1, maintenanceOptions.Value.CleanupIntervalMinutes)), stoppingToken);
        }
    }

    internal async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(storageOptions.Value.UploadDirectory);
        if (string.Equals(Path.TrimEndingDirectorySeparator(root),
                Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root) ?? string.Empty),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("UploadDirectory must be a dedicated non-root directory.");
        if (!Directory.Exists(root)) return 0;
        if (File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("UploadDirectory cannot be a reparse point.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var referenced = (await db.Documents.AsNoTracking().Select(document => document.StorageKey)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-Math.Max(1,
            maintenanceOptions.Value.OrphanGraceMinutes));
        var deleted = 0;
        foreach (var path in EnumerateRegularFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!ManagedKeyPattern.IsMatch(key) || referenced.Contains(key) ||
                File.GetLastWriteTimeUtc(path) > cutoff.UtcDateTime) continue;
            File.Delete(path);
            deleted++;
        }
        return deleted;
    }

    private static IEnumerable<string> EnumerateRegularFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var child in Directory.EnumerateDirectories(directory))
                if (!File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint)) pending.Push(child);
            foreach (var file in Directory.EnumerateFiles(directory))
                if (!File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) yield return file;
        }
    }
}

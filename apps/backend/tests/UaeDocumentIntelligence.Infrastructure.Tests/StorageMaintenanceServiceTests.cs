using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class StorageMaintenanceServiceTests
{
    [Fact]
    public async Task Background_failure_never_forwards_exception_state_or_private_diagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), "UaeDocumentIntelligence.MaintenanceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var logs = new MaintenanceLogs();
            using var service = new StorageMaintenanceService(new FailingScopes(),
                Options.Create(new StorageOptions { SecurityMode = "Development", UploadDirectory = root }),
                Options.Create(new StorageMaintenanceOptions { Enabled = true }), logs);
            await service.StartAsync(default);
            var entry = await logs.Entry.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.StopAsync(default);
            Assert.Null(entry.Error);
            Assert.DoesNotContain("private-cleanup-secret", entry.Message);
            Assert.Contains("diagnostics withheld", entry.Message);
            Assert.DoesNotContain("private-cleanup-secret", entry.State);
        }
        finally { DeleteTestDirectory(root); }
    }

    private sealed class FailingScopes : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("private-cleanup-secret");
    }
    private sealed class MaintenanceLogs : ILogger<StorageMaintenanceService>
    {
        public TaskCompletionSource<(string Message, string State, Exception? Error)> Entry { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) =>
            Entry.TrySetResult((formatter(state, error), state?.ToString() ?? "", error));
    }

    [Fact]
    public async Task Cleanup_deletes_only_old_unreferenced_managed_files()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "UaeDocumentIntelligence.MaintenanceTests",
            Guid.NewGuid().ToString("N"));
        var uploadRoot = Path.Combine(testRoot, "uploads");
        var databasePath = Path.Combine(testRoot, "maintenance.db");
        Directory.CreateDirectory(uploadRoot);
        try
        {
            var organizationId = Guid.NewGuid();
            var referencedDocumentId = Guid.NewGuid();
            var orphanDocumentId = Guid.NewGuid();
            var youngDocumentId = Guid.NewGuid();
            var referencedKey = ManagedKey(organizationId, referencedDocumentId);
            var orphanKey = ManagedKey(organizationId, orphanDocumentId);
            var youngKey = ManagedKey(organizationId, youngDocumentId);
            var referencedPath = WriteFile(uploadRoot, referencedKey);
            var orphanPath = WriteFile(uploadRoot, orphanKey);
            var youngPath = WriteFile(uploadRoot, youngKey);
            var unmanagedPath = WriteFile(uploadRoot, "keep/unmanaged.txt");
            var old = DateTime.UtcNow.AddHours(-2);
            File.SetLastWriteTimeUtc(referencedPath, old);
            File.SetLastWriteTimeUtc(orphanPath, old);
            File.SetLastWriteTimeUtc(unmanagedPath, old);

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "CREATE TABLE documents (storage_key TEXT NOT NULL); " +
                    "INSERT INTO documents (storage_key) VALUES ($key);";
                command.Parameters.AddWithValue("$key", referencedKey);
                await command.ExecuteNonQueryAsync();
            }
            using var provider = CreateProvider(databasePath, uploadRoot);
            var service = new StorageMaintenanceService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new StorageOptions { UploadDirectory = uploadRoot }),
                Options.Create(new StorageMaintenanceOptions
                {
                    Enabled = true,
                    OrphanGraceMinutes = 60,
                    CleanupIntervalMinutes = 30
                }),
                NullLogger<StorageMaintenanceService>.Instance);

            var deleted = await service.CleanupAsync(CancellationToken.None);

            Assert.Equal(1, deleted);
            Assert.True(File.Exists(referencedPath));
            Assert.False(File.Exists(orphanPath));
            Assert.True(File.Exists(youngPath));
            Assert.True(File.Exists(unmanagedPath));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            DeleteTestDirectory(testRoot);
        }
    }

    private static ServiceProvider CreateProvider(string databasePath, string uploadRoot)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = $"Data Source={databasePath}",
            ["Storage:UploadDirectory"] = uploadRoot,
            ["AiService:BaseUrl"] = "http://localhost:8001",
            ["ExtractionWorker:Enabled"] = "false",
            ["StorageMaintenance:Enabled"] = "false"
        };
        var services = new ServiceCollection();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services.BuildServiceProvider();
    }

    private static string ManagedKey(Guid organizationId, Guid documentId) =>
        $"organizations/{organizationId:N}/documents/{documentId:N}/source.pdf";

    private static string WriteFile(string root, string key)
    {
        var path = Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private static void DeleteTestDirectory(string directory)
    {
        var permittedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
            "UaeDocumentIntelligence.MaintenanceTests")).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(directory);
        if (!target.StartsWith(permittedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to remove an unexpected maintenance test directory.");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }
}

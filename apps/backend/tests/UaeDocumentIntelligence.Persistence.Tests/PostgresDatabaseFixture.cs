using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    public const string TestDatabaseName = "document_intelligence_test";
    private const string DefaultAdminConnection =
        "Host=127.0.0.1;Port=55432;Database=postgres;Username=document_intelligence;Password=local_development_only";

    private readonly string _adminConnectionString;
    public string TestConnectionString { get; }

    public PostgresDatabaseFixture()
    {
        var configured = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION") ?? DefaultAdminConnection;
        var admin = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        var test = new NpgsqlConnectionStringBuilder(configured) { Database = TestDatabaseName, Pooling = false };
        if (!string.Equals(test.Database, TestDatabaseName, StringComparison.Ordinal))
            throw new InvalidOperationException("Persistence tests may only use the exact isolated test database.");
        _adminConnectionString = admin.ConnectionString;
        TestConnectionString = test.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await RecreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await DropTestDatabaseAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }

    public async Task ResetToLatestMigrationAsync()
    {
        await using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);
        await migrator.MigrateAsync();
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(TestConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task RecreateDatabaseAsync()
    {
        await DropTestDatabaseAsync();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {TestDatabaseName}", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropTestDatabaseAsync()
    {
        if (!string.Equals(TestDatabaseName, "document_intelligence_test", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to drop an unexpected database.");
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid()",
            connection);
        terminate.Parameters.AddWithValue("database", TestDatabaseName);
        await terminate.ExecuteNonQueryAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {TestDatabaseName}", connection);
        await drop.ExecuteNonQueryAsync();
    }
}

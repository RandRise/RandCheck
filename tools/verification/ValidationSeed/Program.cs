using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;
using ValidationSeed;

// This tool only seeds fictional data in a fixed, owned local validation cluster.
// It cannot accept a connection string, reset a database or change retained application data.
const string database = "uaedi_validation_20261023";
const string admin = "Host=127.0.0.1;Port=55623;Database=postgres;Username=postgres";
const string owner = "Host=127.0.0.1;Port=55623;Database=uaedi_validation_20261023;Username=postgres";
const string role = "uaedi_validation_api_20261023";
const string password = "synthetic_local_validation_only";
if (args.Length != 1 || args[0] is not ("init" or "grow")) throw new ArgumentException("Use init or grow from the repository root.");
var root = Directory.GetCurrentDirectory();
var output = Path.Combine(root, "outputs", "scale-integration-20261023");
var expectedCluster = Path.GetFullPath(Path.Combine(root, "outputs", "library-search-20261023", "postgres"));
await using (var connection = new NpgsqlConnection(admin))
{
    await connection.OpenAsync();
    var actual = (string)(await new NpgsqlCommand("SHOW data_directory", connection).ExecuteScalarAsync())!;
    if (!string.Equals(Path.GetFullPath(actual), expectedCluster, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Refusing an unrecognized PostgreSQL cluster.");
    if (args[0] == "init")
    {
        if (File.Exists(Path.Combine(output, "seed.json"))) throw new InvalidOperationException("Validation seed already exists; it is never reset.");
        // CREATE refuses an existing name; no DROP or overwrite fallback.
        await new NpgsqlCommand($"CREATE DATABASE {database}", connection).ExecuteNonQueryAsync();
    }
    else if (!File.Exists(Path.Combine(output, "seed.json"))) throw new InvalidOperationException("Initialize this validation run first.");
}
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(owner).Options);
if (args[0] == "init")
{
    await db.Database.MigrateAsync();
    await DevelopmentDataSeeder.SeedAsync(db);
    var pair = SyntheticDocuments.Pair();
    db.Documents.AddRange(pair);
    await db.SaveChangesAsync();
    Directory.CreateDirectory(output);
    await File.WriteAllTextAsync(Path.Combine(output, "seed.json"), JsonSerializer.Serialize(new {
        kind = "synthetic-local-validation", database, documents = pair.Select(d => new {
            d.Id, d.OriginalFilename, reviewSessionId = d.ReviewSessions.Single().Id,
            extractionRunId = d.ExtractionRuns.Single().Id })
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    await db.Database.ExecuteSqlRawAsync($"CREATE ROLE {role} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS");
    await ApiDatabasePolicy.ConfigureAsync(db, role, default);
    await File.WriteAllTextAsync(Path.Combine(output, "runtime-settings.json"), JsonSerializer.Serialize(new {
        connection = $"Host=127.0.0.1;Port=55623;Database={database};Username={role};Password={password}", role
    }));
}
var target = args[0] == "init" ? 1000 : 10000;
var count = await db.Documents.CountAsync();
if (count > target) throw new InvalidOperationException("The validation library already exceeds this target; no data is deleted.");
while (count < target)
{
    var end = Math.Min(count + 250, target);
    for (var index = count; index < end; index++) db.Documents.Add(SyntheticDocuments.Library(index));
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    count = end;
}
await db.Database.ExecuteSqlRawAsync("ANALYZE documents; ANALYZE extraction_runs; ANALYZE machine_field_values; ANALYZE review_sessions; ANALYZE review_actions");
Console.WriteLine($"Synthetic validation library ready: {count} documents. Retained application data untouched.");

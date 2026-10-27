using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Persistence;
using UaeDocumentIntelligence.Infrastructure.Services;

// Intentionally fixed to the disposable browser container; no connection override.
const string connection = "Host=127.0.0.1;Port=55438;Database=phase7_browser;Username=ci;Password=disposable_ci_only";
if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] is not ("--append-fixture" or "--schema-only"))
    throw new ArgumentException("Supply repository root and optional --append-fixture or --schema-only");
if (args.Length == 2 && args[1] == "--schema-only")
{
    await using var schemaDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    await schemaDb.Database.MigrateAsync();
    if (await schemaDb.Documents.AnyAsync()) throw new InvalidOperationException("Schema-only setup requires an empty document collection");
    await DevelopmentDataSeeder.SeedAsync(schemaDb);
    Console.WriteLine("Disposable schema and development identity ready; no documents or extraction receipts imported.");
    return;
}
var append = args.Length == 2;
var root = Path.GetFullPath(args[0]);
var fixture = Path.Combine(root, "samples/phase-7/pdf-v0.1.0");
using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture, "manifest.json")));
var receipt = manifest.RootElement.GetProperty("receipts")[0];
var bytes = await File.ReadAllBytesAsync(Path.Combine(fixture, receipt.GetProperty("extractionFile").GetString()!));
if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != receipt.GetProperty("extractionSha256").GetString())
    throw new InvalidDataException("Frozen extraction hash mismatch");
var source = receipt.GetProperty("source");
var sourcePath = Path.Combine(root, "output/pdf/phase-7-language-v0.1.0", source.GetProperty("name").GetString()!);
var sourceBytes = await File.ReadAllBytesAsync(sourcePath);
if (Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant() != source.GetProperty("sha256").GetString())
    throw new InvalidDataException("Fictional source PDF hash mismatch");
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
await db.Database.MigrateAsync();
if (await db.Documents.AnyAsync() && !append) throw new InvalidOperationException("Refusing to modify a populated browser fixture database without --append-fixture");
await DevelopmentDataSeeder.SeedAsync(db);
var org = DevelopmentIdentity.OrganizationId;
var actor = DevelopmentIdentity.ActorId;
var id = Guid.NewGuid();
var storageKey = $"organizations/{org:N}/documents/{id:N}/source.pdf";
var stored = Path.Combine(root, "outputs/phase-7/browser-storage", storageKey);
Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
await File.WriteAllBytesAsync(stored, sourceBytes);
var now = DateTimeOffset.UtcNow;
var doc = new Document(id, org, actor, append ? "فاتورة نهائية — verified-line-positions.pdf" : "فاتورة تجريبية — mixed-native-two-page.pdf", storageKey,
    "application/pdf", sourceBytes.LongLength, source.GetProperty("sha256").GetString()!, now);
using var payload = JsonDocument.Parse(bytes);
var data = payload.RootElement;
var provider = data.GetProperty("provider");
var run = doc.QueueExtraction(Guid.NewGuid(), 1, "local", provider.GetProperty("adapter_version").GetString()!, "2.0", "phase7-browser-fixture", now);
run.Start(now);
foreach (var page in data.GetProperty("pages").EnumerateArray())
    run.AddPage(Guid.NewGuid(), page.GetProperty("page_number").GetInt32(), page.GetProperty("canonical_text").GetString()!, page.GetProperty("rotation_degrees").GetInt16());
foreach (var value in data.GetProperty("fields").EnumerateArray())
{
    var field = run.AddField(Guid.NewGuid(), value.GetProperty("key").GetString()!, value.GetProperty("occurrence").GetInt32(),
        Enum.Parse<FieldValueType>(value.GetProperty("value_type").GetString()!), value.GetProperty("value").GetString()!,
        value.GetProperty("confidence").ValueKind == JsonValueKind.Null ? null : value.GetProperty("confidence").GetDecimal(),
        now, value.GetProperty("normalized_value").GetString());
    foreach (var e in value.GetProperty("evidence").EnumerateArray())
        run.AddEvidence(field, Guid.NewGuid(), e.GetProperty("page_number").GetInt32(), e.GetProperty("quote").GetString(),
            e.GetProperty("start_utf8").GetInt32(), e.GetProperty("end_utf8").GetInt32(),
            e.GetProperty("polygon").ValueKind == JsonValueKind.Null ? null : e.GetProperty("polygon").GetRawText(), e.GetProperty("coordinate_unit").GetString());
}
foreach (var w in data.GetProperty("warnings").EnumerateArray())
    run.AddWarning(Guid.NewGuid(), w.GetProperty("code").GetString()!, Enum.Parse<ExtractionWarningSeverity>(w.GetProperty("severity").GetString()!, true),
        w.GetProperty("safe_summary").GetString()!, OptionalInt(w, "page_number"), w.GetProperty("field_key").GetString(), OptionalInt(w, "occurrence"));
run.RecordProviderMetrics(data.GetProperty("metrics").GetProperty("provider_duration_ms").GetInt64(), run.Pages.Count, null, null, null);
run.CompletePartial(data.GetProperty("document_type").GetString()!, data.GetProperty("language").GetString()!, now,
    provider.GetProperty("model").GetString()!, provider.GetProperty("model_version").GetString()!);
doc.RecordExtractionSuccess(run, now);
var review = doc.OpenReview(Guid.NewGuid(), actor, now);
foreach (var field in run.Fields.Where(f => f.FieldKey != "payment_terms"))
    review.AddAction(Guid.NewGuid(), actor, review.Actions.Count + 1, ReviewActionType.Confirmed, field.FieldKey, field.OccurrenceIndex,
        field.Id, null, null, null, now);
// Fixture preparation only: descriptions were transcribed manually from the PDF.
// Prices remain absent for the browser to add through real review HTTP endpoints.
foreach (var (description, position) in new[] { ("Document review training", 1), ("Workflow configuration session", 2) })
    review.AddAction(Guid.NewGuid(), actor, review.Actions.Count + 1, ReviewActionType.Added, "line_description", position,
        null, FieldValueType.String, description, "وصف من المستند التجريبي — page 1", now);
db.Documents.Add(doc);
await db.SaveChangesAsync();
var output = Path.Combine(root, append ? "outputs/phase-7/browser-seed-final.json" : "outputs/phase-7/browser-seed.json");
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { documentId = doc.Id, reviewId = review.Id,
    sourceSha256 = doc.Sha256, machineLineFields = run.Fields.Count(f => f.FieldKey.StartsWith("line_")),
    seededReviewerDescriptions = 2, pendingBrowserActions = "Correct payment terms; add two Decimal unit prices; complete review" },
    new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Seeded fictional document {doc.Id}; fixture receipt: {output}");
static int? OptionalInt(JsonElement item, string key) => item.GetProperty(key).ValueKind == JsonValueKind.Null ? null : item.GetProperty(key).GetInt32();

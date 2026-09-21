using System.Text.Json;
using UaeDocumentIntelligence.Api.Deployment;

if (args.Length != 2 || args[0] is not ("inspect" or "verify-resources"))
{
    Console.Error.WriteLine("Usage: EnterpriseReadiness inspect|verify-resources <private-effective-config.json>"); return 3;
}
try
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var bytes = await ReadinessFiles.ReadBoundedAsync(args[1], deadline.Token);
    using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
    Unique(json.RootElement);
    var input = json.RootElement.Deserialize<ReadinessInput>(new JsonSerializerOptions {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw new InvalidDataException();
    if (input.Browser is null || input.Backend.ValueKind != JsonValueKind.Object || input.Environment is null) throw new InvalidDataException();
    var report = args[0] == "inspect" ? EnterpriseReadiness.Inspect(input) : await ReadinessResources.VerifyAsync(input, deadline.Token);
    Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    return report.Status == "Blocked" ? 1 : 2;
}
catch
{
    Console.Error.WriteLine("Readiness input or verification failed. Configuration values and exception diagnostics are withheld."); return 3;
}
static void Unique(JsonElement value)
{
    if (value.ValueKind == JsonValueKind.Object)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        { if (!names.Add(property.Name)) throw new InvalidDataException(); Unique(property.Value); }
    }
    else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

// Fixed synthetic plan only. No arbitrary source, model, budget, connection or provider URL arguments.
if (args.Length == 1 && args[0].StartsWith("--evaluation-", StringComparison.Ordinal))
    return await RetrievalEvaluation.RunAsync(args[0]);
if (args.Length != 1 || args[0] is not ("--verify-plan" or "--execute-plan" or "--replay-plan" or "--retrieve-saved-plan")) return 2;
var mode = args[0];
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
try
{
    var root = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (root is not null && !File.Exists(Path.Combine(root.FullName,"samples/phase-5/embedding-pilot-v0.1.0/plan.json"))) root = root.Parent;
    if (root is null) throw new InvalidOperationException();
    var bytes = File.ReadAllBytes(Path.Combine(root.FullName,"samples/phase-5/embedding-pilot-v0.1.0/plan.json"));
    if (CitedEmbeddingStorage.Digest(bytes) != "ba9f727dccf93833ae08e630af5aa4f851e212928bb6f0b265a159a5aaa90099") throw new InvalidOperationException();
    var plan = JsonSerializer.Deserialize<Plan>(bytes, json)!;
    var org = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var actor = Guid.Parse("22222222-2222-2222-2222-222222222222");
    if (plan.SchemaVersion != "embedding-pilot-plan-v1" || plan.OrganizationId != org || plan.ActorId != actor ||
        plan.IndexId != Guid.Parse("704af047-040c-4274-b1f2-052a97c61e7f") || plan.Dimension != 1536 ||
        plan.Model != CitedEmbeddingStorage.Model || plan.Operations.Length != 4) throw new InvalidOperationException();
    var token = mode == "--execute-plan" ? Environment.GetEnvironmentVariable("WORKER_SHARED_SECRET") : "offline-unused-token";
    if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException();
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=25434;Database=phase5_e2e_20260908;Username=document_intelligence;Password=local_development_only",
        ["AiService:BaseUrl"] = "http://127.0.0.1:28158", ["AiService:WorkerToken"] = token
    }).Build();
    var collection = new ServiceCollection().AddInfrastructure(config).AddCitedPilotInfrastructure(config);
    collection.AddSingleton<IExecutionContext>(new Identity(org,actor));
    // Official standard input rate reviewed 2026-09-10; only new embedding admission uses this window.
    // Generation rate settings are not modified. Historical outcome replay is rate-window independent.
    collection.AddSingleton(new CitedEmbeddingSettings(true, new("openai", plan.Model, plan.Model,
        "openai-embedding-small-standard-usd-2026-09-10", new(2026,9,10,0,0,0,TimeSpan.Zero),
        new(2026,9,11,0,0,0,TimeSpan.Zero), .02m, 0m)));
    if (mode != "--execute-plan") collection.AddSingleton<ICitedEmbeddingClient, OfflineClient>();
    LexicalPlan? frozenLexical = null;
    if (mode == "--retrieve-saved-plan")
    {
        var lexicalBytes = File.ReadAllBytes(Path.Combine(root.FullName,"samples/phase-5/embedding-pilot-v0.1.0/lexical-plan.json"));
        if (CitedEmbeddingStorage.Digest(lexicalBytes) != "fbebc28ccc7f5fe1eca82c37f2e817381e7ede6a77ead7d0cec47b0aa6330f07") throw new InvalidOperationException();
        frozenLexical = JsonSerializer.Deserialize<LexicalPlan>(lexicalBytes, json)!;
        frozenLexical.Validate(plan.Operations.Single(x => x.Purpose == "query").Question!, 20);
        collection.AddSingleton<ICitedAiClient>(new FrozenLexicalClient(frozenLexical));
    }
    await using var services = collection.BuildServiceProvider();
    var db = services.GetRequiredService<AppDbContext>();
    if ((await db.Database.GetAppliedMigrationsAsync()).Count() != 15) throw new InvalidOperationException();
    var index = await db.CitedDocumentIndexes.AsNoTracking().SingleAsync(x => x.Id == plan.IndexId && x.OrganizationId == org);
    if (index.DocumentSha256 != plan.DocumentSha256 || index.Status.ToString() != "Ready") throw new InvalidOperationException();
    var sourceStore = services.GetRequiredService<ICitedEmbeddingSources>();
    foreach (var step in plan.Operations.Where(x => x.Purpose == "chunk"))
    {
        var source = await sourceStore.LoadAsync(new(step.OperationId, step.Purpose, [plan.IndexId], step.ChunkIds), default);
        using var parsed = JsonDocument.Parse(source.Utf8);
        foreach (var input in parsed.RootElement.GetProperty("inputs").EnumerateArray())
            if (!step.InputHashes.TryGetValue(input.GetProperty("inputId").GetGuid(), out var hash) ||
                hash != input.GetProperty("inputSha256").GetString()) throw new InvalidOperationException();
    }
    if (mode == "--verify-plan")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { outcome = "plan-verified-no-dispatch", operations = 4, chunks = 9 }));
        return 0;
    }
    if (mode == "--retrieve-saved-plan")
    {
        var query = plan.Operations.Single(x => x.Purpose == "query");
        foreach (var retrievalMode in new[] { "lexical", "vector", "hybrid" })
        {
            var key = $"embedding-pilot-v0.1.0-{retrievalMode}-retrieval-v1";
            var view = retrievalMode == "lexical"
                ? await services.GetRequiredService<RetrieveEvidenceHandler>().HandleAsync(query.Question!, key, [plan.IndexId], 5, default)
                : await services.GetRequiredService<ICitedSemanticRetrievalStore>().RetrieveAsync(query.OperationId, key,
                    retrievalMode, 5, retrievalMode == "hybrid" ? frozenLexical : null, default);
            Console.WriteLine(JsonSerializer.Serialize(new { outcome = "saved-retrieval-complete", mode = retrievalMode,
                view.Id, view.PolicyVersion, view.Replayed, view.RetrievalDurationMs,
                hits = view.Hits.Select(x => new { x.HitId, x.ChunkId, x.IndexId, x.Rank, x.Score }) }, json));
        }
        return 0;
    }
    var coordinator = services.GetRequiredService<CitedEmbeddingCoordinator>();
    foreach (var step in plan.Operations)
    {
        var result = await coordinator.RunAsync(new(step.OperationId, step.Purpose, [plan.IndexId], step.ChunkIds, step.Question), default);
        Console.WriteLine(JsonSerializer.Serialize(new { outcome = "embedding-operation-complete", result }, json));
    }
    return 0;
}
catch (CitedWorkflowException e) { Console.WriteLine(JsonSerializer.Serialize(new { outcome = "stopped", code = e.Code })); return 1; }
catch { Console.WriteLine("{\"outcome\":\"stopped\",\"code\":\"embedding_pilot_failed\"}"); return 1; }

sealed record Identity(Guid OrganizationId, Guid ActorId) : IExecutionContext;
sealed record Plan(string SchemaVersion, Guid OrganizationId, Guid ActorId, Guid IndexId, string DocumentSha256,
    string Model, int Dimension, Step[] Operations);
sealed record Step(Guid OperationId, string Purpose, Guid[] ChunkIds, Dictionary<Guid,string> InputHashes, string? Question);
sealed class OfflineClient : ICitedEmbeddingClient
{
    public Task<byte[]> PrepareAsync(byte[] source, CancellationToken ct) => throw new CitedWorkflowException("offline_only", "Offline replay cannot prepare or dispatch.");
    public Task<byte[]> ProposeAsync(byte[] dispatch, CancellationToken ct) => throw new CitedWorkflowException("offline_only", "Offline replay cannot prepare or dispatch.");
}

sealed class FrozenLexicalClient(LexicalPlan plan) : ICitedAiClient
{
    public Task<ChunkingResult> ChunkAsync(ChunkingSnapshot snapshot, CancellationToken ct) => throw new InvalidOperationException();
    public Task<LexicalPlan> PlanAsync(string question, int topK, CancellationToken ct)
    {
        plan.Validate(question, 20);
        if (topK != 5) throw new InvalidOperationException();
        return Task.FromResult(plan with { TopK = topK });
    }
}

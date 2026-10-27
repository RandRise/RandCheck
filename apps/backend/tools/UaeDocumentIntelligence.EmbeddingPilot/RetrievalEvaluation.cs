using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

internal static class RetrievalEvaluation
{
    private const string PlanHash = "d4176993fac3bc00d0bdbaf8f1ee9e8bb4f7cda6c7b7ff7d50c3366e723e8406";
    private static readonly Guid HeldOperation = Guid.Parse("ccaecf4e-d611-5042-aa64-84d054d42346");
    private static readonly Guid ReplacementOperation = Guid.Parse("5ab2113f-a773-52a5-8509-e5447e140ecd");
    private static readonly Guid Org = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Actor = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly (string Source, Guid Index, string Hash)[] Sources =
    [
        ("quotation-primary", Guid.Parse("1981923e-0baa-4976-bfe8-01c23dcd543a"), "d8b502341da89dacffd763e4ec562a2bdf9fc2e2c5836ea1230a64121b10e636"),
        ("purchase-order-primary", Guid.Parse("44526ce3-5451-47a1-a442-252b8ae98c63"), "fb83571835445e42a7f74402193996acbacb931460cb495ea0beeb4fedcbbb0f"),
        ("invoice-primary", Guid.Parse("f6f4dc55-6d36-420f-98a0-3b5f465e5e81"), "9826c848ebf08933bc93bcc98626745c7eff02c71e1202e507f8f3aa074f42a2")
    ];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static async Task<int> RunAsync(string mode)
    {
        if (mode is not ("--evaluation-export" or "--evaluation-verify" or "--evaluation-execute" or
            "--evaluation-retrieve" or "--evaluation-replay" or "--evaluation-lexical" or
            "--evaluation-retain-unknown" or "--evaluation-continue-verify" or "--evaluation-continue" or
            "--evaluation-continue-replay" or "--evaluation-nonredundant" or "--evaluation-answer-low" or "--evaluation-answer-low-fixed" or "--evaluation-answer-source")) return 2;
        try
        {
            var root = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (root is not null && !File.Exists(Path.Combine(root.FullName,"samples/phase-5/cited-questions-v0.1.0/question-set.json"))) root = root.Parent;
            if (root is null) throw new InvalidOperationException();
            var paid = mode is "--evaluation-execute" or "--evaluation-continue";
            var token = paid ? Environment.GetEnvironmentVariable("WORKER_SHARED_SECRET") : "offline-unused-token";
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=25434;Database=phase5_e2e_20260908;Username=document_intelligence;Password=local_development_only",
                ["AiService:BaseUrl"] = "http://127.0.0.1:28158", ["AiService:WorkerToken"] = token
            }).Build();
            var collection = new ServiceCollection().AddInfrastructure(config).AddCitedPilotInfrastructure(config);
            collection.AddSingleton<IExecutionContext>(new Identity(Org, Actor));
            collection.AddSingleton(new CitedEmbeddingSettings(true, new("openai", CitedEmbeddingStorage.Model,
                CitedEmbeddingStorage.Model, "openai-embedding-small-standard-usd-2026-09-10",
                new(2026,9,10,0,0,0,TimeSpan.Zero), new(2026,9,11,0,0,0,TimeSpan.Zero), .02m, 0m)));
            if (!paid) collection.AddSingleton<ICitedEmbeddingClient, OfflineClient>();
            EvaluationPlan? plan = null;
            if (mode != "--evaluation-export")
            {
                var bytes = File.ReadAllBytes(Path.Combine(root.FullName,"samples/phase-5/retrieval-evaluation-v0.1.0/plan.json"));
                if (CitedEmbeddingStorage.Digest(bytes) != PlanHash) throw new InvalidOperationException();
                plan = JsonSerializer.Deserialize<EvaluationPlan>(bytes, Json)!;
                var dataset = File.ReadAllBytes(Path.Combine(root.FullName,"samples/phase-5/cited-questions-v0.1.0/question-set.json"));
                if (plan.Version != "retrieval-evaluation-v0.1.0" || plan.QuestionSetSha256 != CitedEmbeddingStorage.Digest(dataset) ||
                    plan.Operations.Length != 46 || plan.Cases.Length != 30) throw new InvalidOperationException();
                collection.AddSingleton<ICitedAiClient>(new EvaluationLexicalClient(plan.Cases));
            }
            await using var services = collection.BuildServiceProvider();
            var db = services.GetRequiredService<AppDbContext>();
            if ((await db.Database.GetAppliedMigrationsAsync()).Count() != 15) throw new InvalidOperationException();
            var sourceStore = services.GetRequiredService<ICitedEmbeddingSources>();
            var exports = new List<object>();
            foreach (var source in Sources)
            {
                var index = await db.CitedDocumentIndexes.AsNoTracking().SingleAsync(x => x.Id == source.Index && x.OrganizationId == Org);
                if (index.DocumentSha256 != source.Hash || index.Status.ToString() != "Ready") throw new InvalidOperationException();
                var chunks = await db.DocumentChunks.AsNoTracking().Include(x => x.Spans)
                    .Where(x => x.OrganizationId == Org && x.IndexId == source.Index).ToArrayAsync();
                foreach (var batch in chunks.Chunk(4))
                    await sourceStore.LoadAsync(new(Guid.NewGuid(), "chunk", [source.Index], batch.Select(x => x.Id).ToArray()), default);
                exports.Add(new { sourceId = source.Source, indexId = source.Index, documentSha256 = source.Hash,
                    chunks = chunks.OrderBy(x => x.Id.ToString(), StringComparer.Ordinal).Select(x => new
                    {
                        id = x.Id, text = x.Content, sha256 = x.ContentSha256,
                        spans = x.Spans.OrderBy(s => s.PageNumber).ThenBy(s => s.ByteStart).Select(s => new
                            { s.PageNumber, s.ByteStart, s.ByteEnd, s.Quote })
                    }) });
            }
            var corpus = JsonSerializer.SerializeToUtf8Bytes(exports, Json);
            if (mode == "--evaluation-export") { Console.WriteLine(System.Text.Encoding.UTF8.GetString(corpus)); return 0; }
            if (plan!.CorpusSha256 != CitedEmbeddingStorage.Digest(corpus)) throw new InvalidOperationException();
            foreach (var op in plan.Operations.Where(x => x.Purpose == "chunk"))
            {
                var canonical = await sourceStore.LoadAsync(new(op.OperationId, op.Purpose, op.IndexIds, op.ChunkIds), default);
                using var parsed = JsonDocument.Parse(canonical.Utf8);
                foreach (var input in parsed.RootElement.GetProperty("inputs").EnumerateArray())
                    if (op.InputHashes[input.GetProperty("inputId").GetGuid()] != input.GetProperty("inputSha256").GetString())
                        throw new InvalidOperationException();
            }
            if (mode == "--evaluation-verify")
            {
                Console.WriteLine("{\"outcome\":\"evaluation-verified-no-dispatch\",\"chunks\":60,\"questions\":30,\"operations\":46}");
                return 0;
            }
            var operations = plan.Operations;
            if (mode == "--evaluation-retain-unknown" || mode.StartsWith("--evaluation-continue", StringComparison.Ordinal))
            {
                var manifest = File.ReadAllBytes(Path.Combine(root.FullName,"samples/phase-5/retrieval-evaluation-v0.1.0/continuation.json"));
                if (CitedEmbeddingStorage.Digest(manifest) != "b6d217cf1b595764bb63415d0b29279b0a08ecb805841cded82df6d607a21184")
                    throw new InvalidOperationException();
                if (operations[15].OperationId != HeldOperation) throw new InvalidOperationException();
                var saved = await services.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(HeldOperation, default)
                    ?? throw new InvalidOperationException();
                if (mode == "--evaluation-retain-unknown")
                {
                    var hold = await services.GetRequiredService<ICitedPilotUnknownHold>().RetainAsync(saved.Spend.Id, true, default);
                    Console.WriteLine(JsonSerializer.Serialize(new { outcome="unknown-cost-retained", hold }, Json));
                    return 0;
                }
                if (saved.Spend.State != UaeDocumentIntelligence.Domain.Model.CitedSpendState.HeldUnknown)
                    throw new InvalidOperationException();
                operations = operations.Skip(15).ToArray();
                operations[0] = operations[0] with { OperationId = ReplacementOperation };
                if (mode == "--evaluation-continue-verify")
                {
                    Console.WriteLine("{\"outcome\":\"continuation-verified-no-dispatch\",\"operations\":31,\"retainedUnknownUsd\":0.000001}");
                    return 0;
                }
            }
            if (mode is "--evaluation-execute" or "--evaluation-replay" or "--evaluation-continue" or "--evaluation-continue-replay")
                foreach (var op in operations)
                {
                    var result = await services.GetRequiredService<CitedEmbeddingCoordinator>().RunAsync(
                        new(op.OperationId, op.Purpose, op.IndexIds, op.ChunkIds, op.Question), default);
                    if (mode is "--evaluation-replay" or "--evaluation-continue-replay" && !result.Replayed) throw new InvalidOperationException();
                    Console.WriteLine(JsonSerializer.Serialize(new { outcome = "evaluation-embedding-complete", result }, Json));
                }
            if (mode is "--evaluation-retrieve" or "--evaluation-replay" or "--evaluation-lexical" or "--evaluation-nonredundant" or "--evaluation-answer-low" or "--evaluation-answer-low-fixed" or "--evaluation-answer-source")
                foreach (var item in plan.Cases.Where(c => mode != "--evaluation-answer-low-fixed" || c.QuestionId == "p5-q-001"))
                    foreach (var retrievalMode in mode is "--evaluation-nonredundant" or "--evaluation-answer-low" or "--evaluation-answer-low-fixed" or "--evaluation-answer-source" ? new[] { "hybrid-nonredundant" } :
                        mode == "--evaluation-lexical" ? new[] { "lexical" } : new[] { "lexical", "vector", "hybrid" })
                    {
                        var key = mode == "--evaluation-answer-source" ? $"answer-source-v0.4.0-{item.QuestionId}" :
                            mode == "--evaluation-answer-low-fixed" ? $"answer-low-fixed-v0.3.1-{item.QuestionId}" :
                            mode == "--evaluation-answer-low" ? $"answer-low-v0.3.0-{item.QuestionId}" :
                            $"retrieval-evaluation-v0.1.0-{item.QuestionId}-{retrievalMode}";
                        var result = retrievalMode == "lexical"
                            ? await services.GetRequiredService<RetrieveEvidenceHandler>().HandleAsync(item.Question, key, item.IndexIds, 5, default)
                            : await services.GetRequiredService<ICitedSemanticRetrievalStore>().RetrieveAsync(item.OperationId, key,
                                retrievalMode, 5, retrievalMode != "vector" ? item.LexicalPlan : null, default);
                        if (mode == "--evaluation-replay" && !result.Replayed) throw new InvalidOperationException();
                        Console.WriteLine(JsonSerializer.Serialize(new { outcome = "evaluation-retrieval-complete",
                            item.QuestionId, mode = retrievalMode, result }, Json));
                    }
            return 0;
        }
        catch (CitedWorkflowException e) { Console.WriteLine(JsonSerializer.Serialize(new { outcome = "stopped", code = e.Code })); return 1; }
        catch { Console.WriteLine("{\"outcome\":\"stopped\",\"code\":\"retrieval_evaluation_failed\"}"); return 1; }
    }

    private sealed record EvaluationPlan(string Version, string QuestionSetSha256, string CorpusSha256,
        EvaluationOperation[] Operations, EvaluationCase[] Cases);
    private sealed record EvaluationOperation(Guid OperationId, string Purpose, Guid[] IndexIds, Guid[] ChunkIds,
        Dictionary<Guid,string> InputHashes, string? Question);
    private sealed record EvaluationCase(string QuestionId, Guid OperationId, string Question, Guid[] IndexIds, LexicalPlan LexicalPlan);
    private sealed class EvaluationLexicalClient(EvaluationCase[] cases) : ICitedAiClient
    {
        public Task<ChunkingResult> ChunkAsync(ChunkingSnapshot snapshot, CancellationToken ct) => throw new InvalidOperationException();
        public Task<LexicalPlan> PlanAsync(string question, int topK, CancellationToken ct)
        {
            if (topK != 5) throw new InvalidOperationException();
            var plan = cases.Single(x => x.Question == question).LexicalPlan;
            plan.Validate(question, 20);
            return Task.FromResult(plan with { TopK = 5 });
        }
    }
}

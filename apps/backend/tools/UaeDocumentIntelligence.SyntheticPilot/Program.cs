using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

// Deliberately not an API endpoint or resetting test fixture. One recorded case per invocation.
if (args.Length != 2) return 2;
var retainLow = args[0] == "--retain-low-preflight" && args[1] == "p5-q-001";
var retainSource = args[0] == "--retain-source-generation" && args[1] is "p5-q-001" or "p5-q-005";
var retainCount = args[0] == "--retain-source-count" && int.TryParse(args[1].Replace("p5-q-", ""), out var countCase) && countCase is >= 4 and <= 30;
var typed = args[0] == "--typed-source-case";
var sourceAware = typed || retainCount || retainSource || args[0] is "--source-case" or "--verify-source-case" or "--replay-source-case";
var lowFixed = args[0] is "--low-fixed-case" or "--verify-low-fixed-case" or "--replay-low-fixed-case";
var lowReasoning = sourceAware || lowFixed || retainLow || args[0] is "--low-case" or "--verify-low-case" or "--replay-low-case";
PilotSelection selection;
try
{
    if (lowReasoning || args[0] is "--followup-case" or "--verify-followup-case" or "--replay-followup-case")
    {
        selection = QualityCaseSelection.ReadFollowup(args[1], lowReasoning, lowFixed, sourceAware);
        if (args[0] is "--verify-followup-case" or "--verify-low-case" or "--verify-low-fixed-case" or "--verify-source-case")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { outcome = "manifest-verified-no-dispatch", selection.RunId }));
            return 0;
        }
    }
    else if (args[0] is "--quality-case" or "--verify-quality-case")
    {
        selection = QualityCaseSelection.Read(args[1]);
        if (args[0] == "--verify-quality-case")
        {
            Console.WriteLine(JsonSerializer.Serialize(new { outcome = "manifest-verified-no-dispatch", selection.RunId }));
            return 0;
        }
    }
    else
    {
        if (args[0] != "--synthetic-only" || !Guid.TryParse(args[1], out var id) ||
            id != Guid.Parse("d299b50e-8ef7-423d-9942-e40de003ea5c") &&
            id != Guid.Parse("94b3d1a9-e377-41b2-83c5-6fc960e71563")) return 2;
        selection = new(id, "What amount is due on the invoice?", null,
            [Guid.Parse("704af047-040c-4274-b1f2-052a97c61e7f")]);
    }
}
catch { Console.WriteLine("{\"outcome\":\"stopped\",\"code\":\"case_not_eligible\"}"); return 2; }
var runId = selection.RunId;
var org = Guid.Parse("11111111-1111-1111-1111-111111111111");
var actor = Guid.Parse("22222222-2222-2222-2222-222222222222");
var replayOnly = args[0] is "--replay-followup-case" or "--replay-low-case" or "--replay-low-fixed-case" or "--replay-source-case";
var token = typed || replayOnly || retainLow || retainSource || retainCount ? "offline-unused-token" : Environment.GetEnvironmentVariable("WORKER_SHARED_SECRET");
if (string.IsNullOrWhiteSpace(token)) return 2;
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=25434;Database=phase5_e2e_20260908;Username=document_intelligence;Password=local_development_only",
    ["AiService:BaseUrl"] = "http://127.0.0.1:28157", ["AiService:WorkerToken"] = token
}).Build();
var collection = new ServiceCollection().AddInfrastructure(config).AddCitedPilotInfrastructure(config);
collection.AddSingleton<IExecutionContext>(new PilotIdentity(org, actor));
collection.AddSingleton(new CitedGenerationSettings(true, CitedOpenAiPilot.Rate, true, lowReasoning, sourceAware));
if (typed || replayOnly || retainLow || retainSource || retainCount) collection.AddSingleton<ICitedAnswerClient, OfflineAnswerClient>();
await using var services = collection.BuildServiceProvider();
try
{
    var db = services.GetRequiredService<AppDbContext>();
    if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new InvalidOperationException();
    var run = await db.CitedRetrievalRuns.AsNoTracking().Include(x => x.Sources).SingleOrDefaultAsync(x => x.Id == runId);
    if (run is null || run.OrganizationId != org || run.ActorId != actor ||
        run.Question != selection.Question || selection.RequestKey is not null && run.RequestKey != selection.RequestKey ||
        selection.PolicyVersion is not null && run.PolicyVersion != selection.PolicyVersion ||
        !run.Sources.Select(x => x.IndexId).Order().SequenceEqual(selection.IndexIds.Order())) throw new InvalidOperationException();
    if (replayOnly && (await services.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(runId, default))?.Spend.State !=
        UaeDocumentIntelligence.Domain.Model.CitedSpendState.Settled) throw new InvalidOperationException();
    if (typed)
    {
        var workspace = services.GetRequiredService<CitedFactWorkspace>();
        var typedResult = await workspace.AnswerAsync(runId, default);
        var citations = typedResult.Answer.Claims.SelectMany(c => c.Citations).DistinctBy(c => c.ChunkId).ToArray();
        foreach (var citation in citations)
        {
            var resolved = await workspace.EvidenceAsync(runId, citation.ChunkId, default);
            if (JsonSerializer.Serialize(resolved) != JsonSerializer.Serialize(citation)) throw new InvalidOperationException();
        }
        Console.WriteLine(JsonSerializer.Serialize(new { outcome="typed-unpublished", result=typedResult,
            evidenceReferencesVerified=citations.Length }));
        return 0;
    }
    if (retainCount)
    {
        var saved = await services.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(runId, default)
            ?? throw new InvalidOperationException();
        var hold = await services.GetRequiredService<ICitedPilotUnknownHold>()
            .RetainSourceCountFailureAsync(saved.Spend.Id, true, default);
        Console.WriteLine(JsonSerializer.Serialize(new { outcome="source-count-cost-retained", hold }));
        return 0;
    }
    if (retainSource)
    {
        if (runId != Guid.Parse(args[1] == "p5-q-001" ? "dd54a161-ff31-4863-91de-c12ea35296eb" : "7351e37b-3261-48aa-8294-71c3c521710f")) throw new InvalidOperationException();
        var hold = await services.GetRequiredService<ICitedPilotUnknownHold>().RetainSourceGenerationFailureAsync(
            Guid.Parse(args[1] == "p5-q-001" ? "2971f1ab-5998-4aa6-90d5-7be85ecacac6" : "ba6974b2-ae4f-4c93-b2bc-91a9e227cf82"), true, default);
        Console.WriteLine(JsonSerializer.Serialize(new { outcome="source-generation-cost-retained", hold }));
        return 0;
    }
    if (retainLow)
    {
        if (runId != Guid.Parse("3ae994cd-4973-4c28-9619-5e886cb51e29")) throw new InvalidOperationException();
        var saved = await services.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(runId, default)
            ?? throw new InvalidOperationException();
        var hold = await services.GetRequiredService<ICitedPilotUnknownHold>()
            .RetainLowPreflightFailureAsync(saved.Spend.Id, true, default);
        Console.WriteLine(JsonSerializer.Serialize(new { outcome="local-preflight-cost-retained", hold }));
        return 0;
    }
    var result = await services.GetRequiredService<CitedGenerationCoordinator>().ProposeAsync(runId, default);
    if (replayOnly)
        foreach (var citation in result.Validation.Claims.SelectMany(x => x.Citations))
        {
            var location = await services.GetRequiredService<CitedWorkspaceHandler>()
                .EvidenceAsync(runId, citation.HitId, citation.SpanId, default);
            if (location.IndexId != citation.IndexId || location.ReviewSessionId != citation.ReviewSessionId ||
                location.Page?.Quote != citation.OriginalPageEvidence?.Quote ||
                location.Page?.PageNumber != citation.OriginalPageEvidence?.PageNumber)
                throw new InvalidOperationException();
        }
    Console.WriteLine(JsonSerializer.Serialize(new { outcome = "validated-unpublished", result.ReservationId,
        result.Replayed, result.PublicationAllowed, result.PaymentPolicyVersion, result.ScopePolicyVersion, result.Validation }));
    return 0;
}
catch (CitedWorkflowException error)
{
    Console.WriteLine(JsonSerializer.Serialize(new { outcome = "stopped", code = error.Code }));
    return 1; // Never retry or release a hold automatically.
}
catch
{
    Console.WriteLine("{\"outcome\":\"stopped\",\"code\":\"pilot_failure\"}");
    return 1;
}

sealed record PilotIdentity(Guid OrganizationId, Guid ActorId) : IExecutionContext;
sealed class OfflineAnswerClient : ICitedAnswerClient
{
    public Task<PrivateAnswerPreparation> PrepareAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct)
        => throw new InvalidOperationException();
    public Task<PrivateAnswerResult> ProposeAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct)
        => throw new InvalidOperationException();
}

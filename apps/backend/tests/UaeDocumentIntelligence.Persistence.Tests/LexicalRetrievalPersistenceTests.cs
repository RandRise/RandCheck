using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Live_indexing_and_lexical_retrieval_roundtrip_through_private_http_and_postgres()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=@now WHERE id=@id",
            ("now", DateTimeOffset.UtcNow), ("id", seed.SessionA));
        await using var python = await PrivateCitedServer.StartAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, python.Url);
        Guid indexId, runId;
        async Task<IndexView> CreateIndex()
        {
            await using var scoped = services.CreateAsyncScope();
            return await scoped.ServiceProvider.GetRequiredService<IndexReviewedDocumentHandler>()
                .HandleAsync(seed.DocumentA, seed.SessionA, default);
        }
        var concurrent = await Task.WhenAll(CreateIndex(), CreateIndex());
        Assert.Equal(concurrent[0].Id, concurrent[1].Id);
        Assert.Single(concurrent.Where(x => !x.Replayed));
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IndexReviewedDocumentHandler>();
            var created = await handler.HandleAsync(seed.DocumentA, seed.SessionA, default);
            Assert.True(created.Replayed);
            Assert.Equal(2, created.ChunkCount);
            indexId = created.Id;
            var replay = await handler.HandleAsync(seed.DocumentA, seed.SessionA, default);
            Assert.True(replay.Replayed);
            Assert.Equal(indexId, replay.Id);
            var source = await scope.ServiceProvider.GetRequiredService<ICitedRetrievalStore>()
                .GetSourceAsync(seed.OrganizationA, seed.DocumentA, default);
            Assert.Single(source!.ExtractionRuns.Single().Pages);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<RetrieveEvidenceHandler>();
            var result = await handler.HandleAsync("What is the total?", "live-key", [indexId], 5, default);
            runId = result.Id;
            Assert.Equal("Retrieved", result.Status);
            Assert.Equal(2, result.Hits.Count);
            Assert.All(result.Hits, hit => Assert.Equal(indexId, hit.IndexId));
            Assert.All(result.Hits, hit =>
            {
                Assert.Equal(seed.DocumentA, hit.DocumentId);
                Assert.Equal(seed.RunA, hit.ExtractionRunId);
                Assert.Equal(seed.SessionA, hit.ReviewSessionId);
                Assert.False(string.IsNullOrWhiteSpace(hit.Filename));
            });
            Assert.Contains(result.Hits, x => x.Content == "total[0]: 100.00");
            Assert.All(result.Hits.SelectMany(x => x.Spans), x => Assert.Contains("100.00", x.Quote));
            var replay = await handler.HandleAsync("What is the total?", "live-key", [indexId], 5, default);
            Assert.True(replay.Replayed);
            Assert.Equal(runId, replay.Id);
            Assert.Equal(result.Hits.Select(x => x.HitId), replay.Hits.Select(x => x.HitId));
            var error = await Assert.ThrowsAsync<CitedWorkflowException>(() =>
                handler.HandleAsync("Different question", "live-key", [indexId], 5, default));
            Assert.Equal("request_key_reused", error.Code);
            var empty = await handler.HandleAsync("xyzunmatchedtoken", "no-hit-key", [indexId], 5, default);
            Assert.Equal("Retrieved", empty.Status);
            Assert.Empty(empty.Hits); // no answer/refusal is manufactured at the retrieval layer
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<RetrieveEvidenceHandler>().GetAsync(runId, default);
            Assert.Equal(2, result.Hits.Count);
            Assert.Equal("Retrieved", result.Status);
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            Assert.Empty(await db.CitedAnswers.ToArrayAsync());
            Assert.Equal(1, await db.CitedDocumentIndexes.CountAsync());
        }
        await using var foreignServices = CitedServices(seed.OrganizationB, seed.ActorB, python.Url);
        await using var foreignScope = foreignServices.CreateAsyncScope();
        var foreign = foreignScope.ServiceProvider.GetRequiredService<RetrieveEvidenceHandler>();
        Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() => foreign.GetAsync(runId, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<CitedWorkflowException>(() =>
            foreign.HandleAsync("total", "foreign-key", [indexId], 5, default))).Status);
    }

    [Fact]
    public async Task Lexical_sql_is_corpus_scoped_parameterized_and_rejects_retired_indexes()
    {
        var graph = await SeedCitedGraphAsync();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICitedRetrievalStore>();
        var repository = scope.ServiceProvider.GetRequiredService<ICitedQuestionRepository>();
        var index = (await repository.GetIndexAsync(graph.Seed.OrganizationA, graph.Index.Id, default))!;
        var run = NewSearch(graph.Seed.OrganizationA, graph.Seed.ActorA, index, "total");
        await repository.AddRunAsync(run, default);
        await repository.SaveChangesAsync(default);
        await store.ExecuteLexicalAsync(run, Plan("total", "\"total\""), default);
        Assert.Equal(CitedRetrievalStatus.Retrieved, run.Status);
        Assert.Single(run.Hits);
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
        var duration = run.RetrievalDurationMs;
        db.Entry(run).Property(x => x.RetrievalDurationMs).CurrentValue = duration + 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(run).Property(x => x.RetrievalDurationMs).CurrentValue = duration;
        db.Entry(run).State = EntityState.Unchanged;
        db.Entry(run).Property(x => x.Status).CurrentValue = CitedRetrievalStatus.Running;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(run).Property(x => x.Status).CurrentValue = CitedRetrievalStatus.Retrieved;
        db.Entry(run).State = EntityState.Unchanged;
        Assert.Throws<InvalidOperationException>(() => run.AddHit(Guid.NewGuid(), graph.Chunk, 2, 1, null, null));
        await AssertSqlStateAsync(Npgsql.PostgresErrorCodes.CheckViolation,
            "UPDATE cited_retrieval_runs SET retrieval_duration_ms=NULL WHERE id=@id", ("id", run.Id));
        var injection = NewSearch(graph.Seed.OrganizationA, graph.Seed.ActorA, index, "injection");
        await repository.AddRunAsync(injection, default);
        await repository.SaveChangesAsync(default);
        await store.ExecuteLexicalAsync(injection, Plan("injection", "'; DROP TABLE documents; --"), default);
        Assert.True(await TableExistsAsync("documents"));
        var pending = NewSearch(graph.Seed.OrganizationA, graph.Seed.ActorA, index, "total");
        await repository.AddRunAsync(pending, default);
        await repository.SaveChangesAsync(default);
        index.Retire(true);
        await repository.SaveChangesAsync(default);
        await Assert.ThrowsAsync<CitedWorkflowException>(() => store.ExecuteLexicalAsync(pending, Plan("total", "total"), default));
        await store.FailAsync(graph.Seed.OrganizationA, pending.Id, "retrieval_interrupted", default);
        var reloaded = await repository.GetRunAsync(graph.Seed.OrganizationA, pending.Id, default);
        Assert.Equal(CitedRetrievalStatus.Failed, reloaded!.Status);
        Assert.Empty(reloaded.Hits);
        Assert.Single((await repository.GetRunAsync(graph.Seed.OrganizationA, run.Id, default))!.Hits);
        // A populated rollback must retain evidence, not delete retrieval-only history.
        await using var migrationDb = _database.CreateContext();
        var migrator = migrationDb.GetService<IMigrator>();
        await migrator.MigrateAsync("20260907001143_AddCitedQuestionFoundation");
        var downgraded = await migrationDb.CitedRetrievalRuns.AsNoTracking().Include(x => x.Hits)
            .SingleAsync(x => x.Id == run.Id);
        Assert.Equal(CitedRetrievalStatus.Failed, downgraded.Status);
        Assert.Equal("schema_downgrade", downgraded.FailureCode);
        Assert.Single(downgraded.Hits);
        await migrator.MigrateAsync();
    }

    private ServiceProvider CitedServices(Guid organizationId, Guid actorId, string url, ILoggerProvider? logCapture = null,
        bool openAi = false, bool pilotClock = false, bool registerPilot = false)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = _database.TestConnectionString,
            ["AiService:BaseUrl"] = url, ["AiService:WorkerToken"] = "test-only-cited-token"
        }).Build();
        var services = new ServiceCollection().AddInfrastructure(config);
        if (registerPilot) services.AddCitedPilotInfrastructure(config);
        if (openAi)
        {
            services.AddSingleton(new CitedGenerationSettings(true, CitedOpenAiPilot.Rate, true));
        }
        if (openAi || pilotClock)
        {
            // AddDbContext's first options registration wins in this EF version.
            // Replace the resolved typed options so the ledger's fresh contexts
            // also receive the fixture clock, rather than only a scoped context.
            services.AddSingleton(new DbContextOptionsBuilder<UaeDocumentIntelligence.Infrastructure.Persistence.AppDbContext>()
                .UseNpgsql(_database.TestConnectionString)
                .AddInterceptors(new PilotFixtureClock()).Options);
        }
        if (logCapture is not null)
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logCapture));
        services.AddSingleton<IExecutionContext>(new CitedExecution(organizationId, actorId));
        return services.BuildServiceProvider();
    }
    private ServiceProvider CitedPilotServices(Guid organizationId, Guid actorId, string url, ILoggerProvider? logCapture = null,
        bool openAi = false, bool pilotClock = false) =>
        CitedServices(organizationId, actorId, url, logCapture, openAi, pilotClock, registerPilot: true);
    private sealed record CitedExecution(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private static LexicalPlan Plan(string question, string text) => new("1.0", LexicalPlan.CurrentVersion,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(question))).ToLowerInvariant(), "english", text, 5);
    private static CitedRetrievalRun NewSearch(Guid organizationId, Guid actorId, CitedDocumentIndex index, string question) =>
        new(Guid.NewGuid(), organizationId, actorId, question, Guid.NewGuid().ToString(), LexicalPlan.CurrentVersion,
            "{}", [index], DateTimeOffset.UtcNow);

    private sealed class PrivateCitedServer(Process process, string url) : IAsyncDisposable
    {
        public string Url { get; } = url;
        public static async Task<PrivateCitedServer> StartAsync(bool fakeAnswers = false, string? openAiOutcome = null)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "apps/api/app/main.py"))) root = root.Parent;
            Assert.NotNull(root);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("PHASE5_PYTHON") ?? "python")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-m", "uvicorn", openAiOutcome is not null ? "cited_openai_fixture_server:app" :
                         fakeAnswers ? "cited_answer_fixture_server:app" : "app.main:app",
                         "--app-dir", Path.Combine(root!.FullName, fakeAnswers || openAiOutcome is not null ? "apps/api/tests" : "apps/api"),
                         "--host", "127.0.0.1", "--port", port.ToString(), "--no-access-log" }) start.ArgumentList.Add(arg);
            start.Environment["WORKER_SHARED_SECRET"] = "test-only-cited-token";
            if (openAiOutcome is not null) start.Environment["PILOT_FIXTURE_OUTCOME"] = openAiOutcome;
            start.Environment["PYTHONPATH"] = Path.Combine(root.FullName, "apps/api");
            var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            _ = process.StandardOutput.ReadToEndAsync();
            var server = new PrivateCitedServer(process, $"http://127.0.0.1:{port}");
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            try
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    if (process.HasExited) throw new InvalidOperationException(await error);
                    try { if ((await client.GetAsync(server.Url + "/health")).IsSuccessStatusCode) return server; }
                    catch (HttpRequestException) { }
                    catch (TaskCanceledException) { }
                    await Task.Delay(100);
                }
                throw new TimeoutException("Private test server did not start.");
            }
            catch { await server.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            process.Dispose();
        }
    }
}

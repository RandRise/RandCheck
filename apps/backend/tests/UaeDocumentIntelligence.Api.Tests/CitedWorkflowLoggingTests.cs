using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Api.Tests;

public sealed class CitedWorkflowLoggingTests
{
    private const string Private = "private-invoice-content-8629";
    private const string Credential = "private-worker-token-8629";
    private const string Metadata = "private-url-metadata-8629";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("/api/cited-questions")]
    [InlineData("/api/cited-retrievals")]
    public async Task Successful_retrieval_replay_history_and_evidence_keep_payloads_out_of_logs(string route)
    {
        var graph = new Graph();
        var logs = new Capture();
        using var factory = Factory(graph, logs);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(route, graph.Body());
        await AssertSuccess(response);
        Assert.Single(graph.Runs);
        Assert.Equal(1, graph.OutboundCalls);
        var run = graph.Runs.Single();
        using var replay = await client.PostAsJsonAsync(route, graph.Body());
        await AssertSuccess(replay);
        Assert.Equal(1, graph.OutboundCalls);
        foreach (var path in new[] { $"/api/cited-questions/{run.Id}", $"/api/cited-retrievals/{run.Id}", "/api/cited-sources" })
        {
            using var saved = await client.GetAsync(path);
            await AssertSuccess(saved);
        }
        foreach (var hit in run.Hits)
        {
            var chunk = graph.Index.Chunks.Single(x => x.Id == hit.ChunkId);
            var query = chunk.Kind == DocumentChunkKind.PageText ? $"?spanId={chunk.Spans.Single().Id}" : "";
            using var evidence = await client.GetAsync($"/api/cited-questions/{run.Id}/hits/{hit.Id}/evidence{query}");
            await AssertSuccess(evidence);
        }
        AssertPrivateLogs(logs);
        Assert.Contains(logs.Entries, x => x.Contains("Private cited operation completed:"));
        Assert.DoesNotContain(logs.Entries, x => x.Contains("System.Net.Http.HttpClient.ICitedAiClient"));
    }

    [Fact]
    public async Task No_hit_refusal_and_history_keep_question_out_of_logs()
    {
        var graph = new Graph { NoHits = true };
        var logs = new Capture();
        using var factory = Factory(graph, logs);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/cited-questions", graph.Body());
        await AssertSuccess(response);
        Assert.Contains("InsufficientEvidence", await response.Content.ReadAsStringAsync());
        using var saved = await client.GetAsync($"/api/cited-questions/{graph.Runs.Single().Id}");
        await AssertSuccess(saved);
        AssertPrivateLogs(logs);
    }

    [Theory]
    [InlineData("reject", 502)]
    [InlineData("invalid", 502)]
    [InlineData("transport", 503)]
    [InlineData("timeout", 504)]
    public async Task Private_client_failures_do_not_expose_response_or_exception_content(string failure, int status)
    {
        var graph = new Graph { Failure = failure };
        var logs = new Capture();
        using var factory = Factory(graph, logs);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/cited-questions", graph.Body());
        Assert.Equal(status, (int)response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.DoesNotContain(Private, await response.Content.ReadAsStringAsync());
        Assert.Empty(graph.Runs);
        Assert.Equal(1, graph.OutboundCalls);
        AssertPrivateLogs(logs);
    }

    [Theory]
    [InlineData("find")]
    [InlineData("lexical")]
    [InlineData("load")]
    [InlineData("page")]
    [InlineData("catalog")]
    public async Task Unexpected_downstream_failures_have_safe_responses_and_logs(string failure)
    {
        var graph = new Graph();
        var logs = new Capture();
        using var factory = Factory(graph, logs);
        using var client = factory.CreateClient();
        if (failure is "load" or "page")
        {
            using var setup = await client.PostAsJsonAsync("/api/cited-questions", graph.Body());
            await AssertSuccess(setup);
        }
        graph.Failure = failure;
        var path = failure switch
        {
            "load" => $"/api/cited-questions/{graph.Runs.Single().Id}",
            "page" => $"/api/cited-questions/{graph.Runs.Single().Id}/hits/{graph.Runs.Single().Hits.First().Id}/evidence?spanId={graph.PageChunk.Spans.Single().Id}",
            "catalog" => "/api/cited-sources",
            _ => "/api/cited-questions"
        };
        using var response = failure is "find" or "lexical"
            ? await client.PostAsJsonAsync(path, graph.Body()) : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("cited_request_failed", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(Private, await response.Content.ReadAsStringAsync());
        AssertPrivateLogs(logs);
        Assert.Contains(logs.Entries, x => x.Contains("cited_request_failed"));
        if (failure == "lexical") Assert.Equal("retrieval_interrupted", graph.Runs.Single().FailureCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Metadata_visibility_is_separate_from_payload_privacy(bool verbose)
    {
        var graph = new Graph();
        var logs = new Capture();
        using var factory = Factory(graph, logs, verbose);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Request-ID", Metadata);
        using var response = await client.PostAsJsonAsync($"/api/cited-questions?diagnostic={Metadata}", graph.Body());
        await AssertSuccess(response);
        Assert.Equal(Metadata, response.Headers.GetValues("X-Request-ID").Single());
        AssertPrivateLogs(logs);
        // Characterize a known limitation, not a claim that arbitrary debug telemetry is private.
        Assert.Equal(verbose, logs.Entries.Any(x => x.Contains(Metadata)));
    }

    private static async Task AssertSuccess(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains(Private, await response.Content.ReadAsStringAsync());
    }

    private static void AssertPrivateLogs(Capture logs)
    {
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.Entries, entry => entry.Contains(Private) || entry.Contains(Credential));
    }

    private static WebApplicationFactory<Program> Factory(Graph graph, Capture logs, bool verbose = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UsePhase5DevelopmentIdentity()
            .ConfigureLogging(logging =>
            {
                logging.AddProvider(logs);
                if (verbose) logging.AddFilter<Capture>((_, _) => true);
            })
            .ConfigureServices(services =>
            {
                services.RemoveAll<ICitedRetrievalStore>();
                services.RemoveAll<ICitedQuestionRepository>();
                services.RemoveAll<ICitedWorkspaceStore>();
                services.RemoveAll<IExecutionContext>();
                services.AddSingleton<ICitedRetrievalStore>(graph);
                services.AddSingleton<ICitedQuestionRepository>(graph);
                services.AddSingleton<ICitedWorkspaceStore>(graph);
                services.AddSingleton<IExecutionContext>(graph);
                services.PostConfigure<AiServiceOptions>(options => options.WorkerToken = Credential);
                // Keep the real typed client and logging pipeline; replace only external transport.
                services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions
                    .Add(handler => handler.PrimaryHandler = new PrivateTransport(graph)));
            }));

    private sealed class PrivateTransport(Graph graph) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            graph.OutboundCalls++;
            Assert.Equal(Credential, request.Headers.GetValues("X-Worker-Token").Single());
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var question = body.GetProperty("question").GetString()!;
            Assert.Contains(Private, question);
            if (graph.Failure == "transport") throw new HttpRequestException(Private);
            if (graph.Failure == "timeout") throw new OperationCanceledException(Private);
            var response = new HttpResponseMessage(graph.Failure == "reject" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
            response.Headers.Add("X-Private-Response", Private);
            response.Content = graph.Failure is "reject" or "invalid" ? new StringContent("{\"" + Private + "\":") :
                JsonContent.Create(new LexicalPlan("1.0", LexicalPlan.CurrentVersion,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(question))).ToLowerInvariant(),
                    "english", question, body.GetProperty("topK").GetInt32()));
            return response;
        }
    }

    private sealed class Graph : ICitedRetrievalStore, ICitedQuestionRepository, ICitedWorkspaceStore, IExecutionContext
    {
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid ActorId { get; } = Guid.NewGuid();
        public CitedDocumentIndex Index { get; }
        public DocumentChunk PageChunk { get; }
        public ExtractionPage Page { get; }
        public ReviewAction Action { get; }
        public List<CitedRetrievalRun> Runs { get; } = [];
        public string? Failure { get; set; }
        public bool NoHits { get; init; }
        public int OutboundCalls { get; set; }
        public object Body() => new { question = Private + " total?", requestKey = Private, indexIds = new[] { Index.Id }, topK = 5 };

        public Graph()
        {
            var now = DateTimeOffset.UtcNow;
            var document = new Document(Guid.NewGuid(), OrganizationId, ActorId, Private + ".pdf", "fixture.pdf", "application/pdf", 100, new string('a', 64), now);
            var extraction = document.QueueExtraction(Guid.NewGuid(), 1, "fixture", "1", "1", "fixture", now);
            extraction.Start(now);
            Page = extraction.AddPage(Guid.NewGuid(), 1, Private + " Total AED 100.00", 0);
            var field = extraction.AddField(Guid.NewGuid(), "supplier", 0, FieldValueType.String, Private, 1m, now);
            extraction.AddEvidence(field, Guid.NewGuid(), 1);
            extraction.RecordProviderMetrics(1, 1, null, null, null);
            extraction.Succeed("invoice", "en", now, "fixture", "1");
            document.RecordExtractionSuccess(extraction, now);
            var review = document.OpenReview(Guid.NewGuid(), ActorId, now);
            Action = review.AddAction(Guid.NewGuid(), ActorId, 1, ReviewActionType.Confirmed, "supplier", 0, field.Id, null, null, Private, now);
            review.Complete(extraction.Fields, now);
            Index = new CitedDocumentIndex(Guid.NewGuid(), document, review, "fixture", "1", now);
            PageChunk = Index.AddPageChunk(Guid.NewGuid(), 0, Page, 0, Encoding.UTF8.GetByteCount(Page.CanonicalText));
            Index.AddReviewedFact(Guid.NewGuid(), 1, review, "supplier", 0, extraction);
            Index.Publish();
        }

        private void FailAt(string point) { if (Failure == point) throw new InvalidOperationException(Private); }
        public Task<CitedRetrievalRun?> FindRequestAsync(Guid org, Guid actor, string key, CancellationToken ct)
        { FailAt("find"); return Task.FromResult(Runs.SingleOrDefault(x => x.OrganizationId == org && x.ActorId == actor && x.RequestKey == key)); }
        public Task<CitedDocumentIndex?> GetIndexHeaderAsync(Guid org, Guid id, CancellationToken ct) =>
            Task.FromResult(org == OrganizationId && id == Index.Id ? Index : null);
        public Task<CitedDocumentIndex?> GetIndexAsync(Guid org, Guid id, CancellationToken ct) => GetIndexHeaderAsync(org, id, ct);
        public Task<IReadOnlyList<DocumentChunk>> GetHitChunksAsync(CitedRetrievalRun run, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DocumentChunk>>(Index.Chunks.ToArray());
        public Task ExecuteLexicalAsync(CitedRetrievalRun run, LexicalPlan plan, CancellationToken ct)
        {
            FailAt("lexical");
            if (!NoHits) foreach (var chunk in Index.Chunks) run.AddHit(Guid.NewGuid(), chunk, chunk.Ordinal + 1, 1, null, null);
            run.MarkRetrieved(1);
            return Task.CompletedTask;
        }
        public Task FailAsync(Guid org, Guid id, string code, CancellationToken ct)
        { Runs.Single(x => x.Id == id).Fail(code, DateTimeOffset.UtcNow); return Task.CompletedTask; }
        public Task AddRunAsync(CitedRetrievalRun run, CancellationToken ct) { Runs.Add(run); return Task.CompletedTask; }
        public Task<CitedRetrievalRun?> GetRunAsync(Guid org, Guid id, CancellationToken ct)
        { FailAt("load"); return Task.FromResult(Runs.SingleOrDefault(x => x.OrganizationId == org && x.Id == id)); }
        public Task<CitedQuestionSaveOutcome> SaveChangesAsync(CancellationToken ct) => Task.FromResult(CitedQuestionSaveOutcome.Saved);
        public Task<ReviewedSourcesView> ListSourcesAsync(Guid org, int offset, CancellationToken ct)
        {
            FailAt("catalog");
            return Task.FromResult(new ReviewedSourcesView([new(Index.DocumentId, Index.Filename, Index.ReviewSessionId, Index.ExtractionRunId, 1, DateTimeOffset.UtcNow)], null));
        }
        public Task<ExtractionPage?> GetPageAsync(Guid org, Guid run, int page, CancellationToken ct)
        { FailAt("page"); return Task.FromResult<ExtractionPage?>(Page); }
        public Task<ReviewAction?> GetActionAsync(Guid org, Guid review, Guid action, CancellationToken ct) => Task.FromResult<ReviewAction?>(Action);
        public Task<Document?> GetSourceAsync(Guid org, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<CitedDocumentIndex?> FindIndexAsync(Guid org, Guid review, ChunkingProfile profile, CancellationToken ct) => throw new NotSupportedException();
        public Task AddIndexAsync(CitedDocumentIndex index, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
        public void Dispose() { }
        private sealed class Logger(Capture owner, string category) : ILogger
        {
            public bool IsEnabled(LogLevel level) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull { Record(state); return null; }
            private void Record<TState>(TState state)
            {
                owner.Entries.Enqueue($"{category}: {state}");
                if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                    foreach (var field in fields) owner.Entries.Enqueue($"{field.Key}: " +
                        (field.Value is IEnumerable<string> values ? string.Join(",", values) : field.Value));
            }
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            { Record(state); owner.Entries.Enqueue(formatter(state, error) + error); }
        }
    }
}

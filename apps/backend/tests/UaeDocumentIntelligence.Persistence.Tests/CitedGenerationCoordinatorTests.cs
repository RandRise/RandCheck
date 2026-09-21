using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Fact]
    public async Task Generation_default_and_live_models_are_blocked_before_reservation()
    {
        var graph = await SeedCitedGraphAsync();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        var client = new AnswerReply();
        await SpendDenied("generation_disabled", () => services.GetRequiredService<CitedGenerationCoordinator>().ProposeAsync(graph.Request.Id, default));
        await SpendDenied("generation_live_not_ready", () => Coordinator(services, client,
            new(true, SpendCommand().Rate with { Provider = "openai" })).ProposeAsync(graph.Request.Id, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Generation_real_python_roundtrip_settles_but_never_publishes_or_redispatches()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var python = await PrivateCitedServer.StartAsync(fakeAnswers: true);
        var settings = new CitedGenerationSettings(true, SpendCommand().Rate);
        await using (var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, python.Url))
        {
            var result = await Coordinator(services, services.GetRequiredService<ICitedAnswerClient>(), settings).ProposeAsync(runId, default);
            Assert.False(result.PublicationAllowed);
            Assert.False(result.Validation.SemanticSupportEstablished);
            Assert.Single(result.Validation.Claims);
            var inputTokens = await ScalarAsync<int>("SELECT maximum_input_tokens FROM cited_spend_reservations");
            Assert.InRange(inputTokens, 101, 8000);
            Assert.Equal(decimal.Ceiling(inputTokens * 0.05m + 50 * 0.40m) / 1_000_000m,
                await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        }
        await using var restarted = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, python.Url);
        var replay = await Coordinator(restarted, new AnswerReply((_, _) => throw new InvalidOperationException()), settings)
            .ProposeAsync(runId, default);
        Assert.True(replay.Replayed);
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));
    }

    [Fact]
    public async Task Generation_concurrent_scopes_dispatch_once_after_commit()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var settings = new CitedGenerationSettings(true, SpendCommand().Rate);
        var client = new AnswerReply(async (reply, _) =>
        {
            Assert.Equal("Dispatched", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
            Assert.Equal(2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
            Assert.Equal(1000, await ScalarAsync<int>("SELECT maximum_input_tokens FROM cited_spend_reservations"));
            Assert.Equal(0.00165m, await ScalarAsync<decimal>("SELECT reserved_usd FROM cited_spend_reservations"));
            return reply;
        });
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
            try { return !(await Coordinator(services, client, settings).ProposeAsync(runId, default)).Replayed; }
            catch (CitedWorkflowException e) when (e.Code == "generation_already_dispatched") { return false; }
        }));
        Assert.Equal(1, results.Count(x => x));
        Assert.Equal(1, client.Calls);
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Generation_foreign_scope_inactive_actor_and_retired_source_never_dispatch()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply();
        await using var foreign = CitedServices(graph.Seed.OrganizationB, graph.Seed.ActorB, "http://127.0.0.1:1");
        await SpendDenied("retrieval_not_found", () => Coordinator(foreign, client).ProposeAsync(runId, default));
        await ExecuteAsync("UPDATE actors SET is_active=false WHERE id=@id", ("id", graph.Seed.ActorA));
        await using var owner = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("reservation_not_found", () => Coordinator(owner, client).ProposeAsync(runId, default));
        await ExecuteAsync("UPDATE actors SET is_active=true WHERE id=@id", ("id", graph.Seed.ActorA));
        await ExecuteAsync("UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id", graph.Index.Id));
        await SpendDenied("evidence_unavailable", () => Coordinator(owner, client).ProposeAsync(runId, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, client.Preparations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_provider_failure_retains_hold_and_does_not_retry(bool timeout)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var settings = new CitedGenerationSettings(true, SpendCommand().Rate);
        var client = new AnswerReply((_, _) => throw (timeout ? new TimeoutException("SECRET INVOICE") : new InvalidOperationException("SECRET INVOICE")));
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("generation_outcome_uncertain", () => Coordinator(services, client, settings).ProposeAsync(runId, default));
        await SpendDenied("generation_already_dispatched", () => Coordinator(services, client, settings).ProposeAsync(runId, default));
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Generation_caller_cancellation_after_dispatch_keeps_uncertain_hold()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        using var cancellation = new CancellationTokenSource();
        var client = new AnswerReply((reply, ct) =>
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(reply);
        });
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Coordinator(services, client).ProposeAsync(runId, cancellation.Token));
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(1, client.Calls);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("usage")]
    [InlineData("model")]
    [InlineData("incomplete")]
    public async Task Generation_wrong_reply_binding_does_not_settle_or_publish(string defect)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply((reply, _) => Task.FromResult(defect switch
        {
            "usage" => reply with { OutputTokens = 4001 },
            "model" => reply with { Model = reply.Model with { Version = "wrong" } },
            "incomplete" => reply with { Outcome = "truncated" },
            _ => reply with { RequestSha256 = new string('b', 64) }
        }));
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("generation_outcome_uncertain", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal("Uncertain", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
    }

    [Fact]
    public async Task Generation_invalid_citations_are_charged_but_not_accepted()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply((reply, _) => Task.FromResult(reply with { ProposalUtf8 = "SECRET INVALID JSON"u8.ToArray() }));
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("answer_proposal_not_accepted", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal("Settled", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0.000025m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));
    }

    [Fact]
    public async Task Generation_source_revoked_during_provider_call_blocks_returned_proposal()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        var client = new AnswerReply(async (reply, _) =>
        {
            await ExecuteAsync("UPDATE cited_document_indexes SET status='Quarantined' WHERE id=@id", ("id", graph.Index.Id));
            return reply;
        });
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("answer_proposal_not_accepted", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal("Settled", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Generation_dispatch_commit_failure_never_calls_provider()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await ExecuteAsync("ALTER TABLE cited_spend_events ADD CONSTRAINT injected_dispatch_failure CHECK (sequence=1)");
        var client = new AnswerReply();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("spend_store_unavailable", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal("Reserved", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
    }

    [Fact]
    public async Task Generation_settlement_and_cleanup_failure_leave_dispatch_hold()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await ExecuteAsync("ALTER TABLE cited_spend_events ADD CONSTRAINT injected_settlement_failure CHECK (sequence<=2)");
        var client = new AnswerReply();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        await SpendDenied("generation_outcome_uncertain", () => Coordinator(services, client).ProposeAsync(runId, default));
        Assert.Equal("Dispatched", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Generation_source_aware_real_python_roundtrip_binds_metadata_and_replays_without_publication()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var python = await PrivateCitedServer.StartAsync(fakeAnswers: true);
        var settings = new CitedGenerationSettings(true, SpendCommand().Rate, SourceAwarePilot: true);
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, python.Url);
        var result = await Coordinator(services, services.GetRequiredService<ICitedAnswerClient>(), settings).ProposeAsync(runId, default);
        Assert.False(result.PublicationAllowed);
        Assert.False(result.Validation.SemanticSupportEstablished);
        Assert.Equal(CitedAnswerValidator.SourceVersion, result.Validation.ValidatorVersion);
        Assert.Equal(graph.Chunk.Content, result.Validation.Claims.Single().Citations.Single().SupportText);
        var saved = await services.GetRequiredService<ICitedGenerationJournal>().GetSavedAsync(runId, default);
        using var dispatch = JsonDocument.Parse(saved!.PreparationUtf8!);
        using var source = JsonDocument.Parse(dispatch.RootElement.GetProperty("requestUtf8").GetBytesFromBase64());
        var metadata = source.RootElement.GetProperty("evidence")[0].GetProperty("source");
        Assert.Equal(graph.Index.DocumentId, metadata.GetProperty("documentId").GetGuid());
        Assert.Equal(graph.Index.Id, metadata.GetProperty("indexId").GetGuid());
        Assert.Equal(graph.Chunk.Spans.Single().Quote, metadata.GetProperty("spans")[0].GetProperty("quote").GetString());
        Assert.Equal("cited-answer-prompt-v4", dispatch.RootElement.GetProperty("preparation").GetProperty("promptVersion").GetString());
        var replay = await Coordinator(services, new AnswerReply((_, _) => throw new InvalidOperationException()), settings)
            .ProposeAsync(runId, default);
        Assert.True(replay.Replayed);
        Assert.Equal(result.Validation.Claims.Single().Citations.Single().SupportText,
            replay.Validation.Claims.Single().Citations.Single().SupportText);
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));
    }

    [Fact]
    public async Task Generation_source_aware_rejects_legacy_preparation_before_reservation()
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        var client = new AnswerReply();
        await SpendDenied("invalid_prompt_preparation", () => Coordinator(services, client,
            new(true, SpendCommand().Rate, SourceAwarePilot: true)).ProposeAsync(runId, default));
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_reservations"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_source_aware_wrong_excerpt_or_downgrade_settles_but_rejects_and_never_redispatches(bool downgrade)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
        var settings = new CitedGenerationSettings(true, SpendCommand().Rate, SourceAwarePilot: true);
        var client = new AnswerReply((reply, _) =>
        {
            var proposal = System.Text.Json.Nodes.JsonNode.Parse(reply.ProposalUtf8)!;
            if (!downgrade)
            {
                proposal["schemaVersion"] = "1.1";
                proposal["claims"]![0]!["citations"]![0]!["supportText"] = "fabricated amount 999999";
            }
            return Task.FromResult(reply with { ProposalUtf8 = JsonSerializer.SerializeToUtf8Bytes(proposal) });
        }) { ChangePreparation = p => Task.FromResult(p with { PromptVersion = "cited-answer-prompt-v4" }) };
        await SpendDenied("answer_proposal_not_accepted", () => Coordinator(services, client, settings).ProposeAsync(runId, default));
        await SpendDenied("answer_proposal_not_accepted", () => Coordinator(services, client, settings).ProposeAsync(runId, default));
        Assert.Equal(1, client.Calls);
        Assert.Equal("Settled", await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(0.000025m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));
    }

    private async Task<Guid> GenerationRun(CitedSeed graph)
    {
        await using var db = _database.CreateContext();
        var index = await db.CitedDocumentIndexes.SingleAsync(x => x.Id == graph.Index.Id);
        var chunk = await db.DocumentChunks.Include(x => x.Spans).SingleAsync(x => x.Id == graph.Chunk.Id);
        var run = NewSearch(graph.Seed.OrganizationA, graph.Seed.ActorA, index, "What is the total?");
        run.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        run.MarkRetrieved(1);
        db.CitedRetrievalRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    private static CitedGenerationCoordinator Coordinator(IServiceProvider services, ICitedAnswerClient client,
        CitedGenerationSettings? settings = null) => new(services.GetRequiredService<ICitedQuestionRepository>(),
        services.GetRequiredService<ICitedRetrievalStore>(), services.GetRequiredService<ICitedGenerationLedger>(),
        client, services.GetRequiredService<IExecutionContext>(), settings ?? new(true, SpendCommand().Rate),
        services.GetRequiredService<ICitedGenerationJournal>());

    private sealed class AnswerReply(Func<PrivateAnswerResult, CancellationToken, Task<PrivateAnswerResult>>? change = null) : ICitedAnswerClient
    {
        private int _calls;
        public int Calls => _calls;
        public int Preparations;
        public byte[]? LastRequestUtf8;
        public Func<PrivateAnswerPreparation, Task<PrivateAnswerPreparation>>? ChangePreparation;
        public async Task<PrivateAnswerPreparation> PrepareAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct)
        {
            Interlocked.Increment(ref Preparations);
            LastRequestUtf8 = requestUtf8.ToArray();
            using var document = JsonDocument.Parse(requestUtf8);
            var model = document.RootElement.GetProperty("model");
            var result = new PrivateAnswerPreparation
            {
                SchemaVersion = "1.0", RequestSha256 = Convert.ToHexString(SHA256.HashData(requestUtf8.Span)).ToLowerInvariant(),
                Model = new(model.GetProperty("providerAlias").GetString()!, model.GetProperty("model").GetString()!, model.GetProperty("version").GetString()!),
                PromptSha256 = new string('a', 64), PromptVersion = "cited-answer-prompt-v3",
                CounterVersion = "test-utf8-bytes-v1", InputTokens = 1000, MaximumOutputTokens = 4000
            };
            return ChangePreparation is null ? result : await ChangePreparation(result);
        }
        public async Task<PrivateAnswerResult> ProposeAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            using var dispatch = JsonDocument.Parse(requestUtf8);
            var sourceBytes = dispatch.RootElement.GetProperty("requestUtf8").GetBytesFromBase64();
            using var document = JsonDocument.Parse(sourceBytes);
            var body = document.RootElement;
            var item = body.GetProperty("evidence")[0];
            var model = body.GetProperty("model");
            var proposal = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = "1.0", status = "Answered", reasonCode = (string?)null,
                claims = new[] { new { text = "Synthetic total is 100.00.", citations = new[] { new
                { hitId = item.GetProperty("hitId").GetGuid(), spanId = item.GetProperty("spanIds")[0].GetGuid() } } } },
                unansweredParts = Array.Empty<string>()
            });
            var result = new PrivateAnswerResult
            {
                SchemaVersion = "1.0", RequestSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant(),
                PromptSha256 = dispatch.RootElement.GetProperty("preparation").GetProperty("promptSha256").GetString()!,
                Model = new(model.GetProperty("providerAlias").GetString()!, model.GetProperty("model").GetString()!, model.GetProperty("version").GetString()!),
                Outcome = "completed", ProposalUtf8 = proposal, InputTokens = 100, OutputTokens = 50
            };
            return change is null ? result : await change(result, ct);
        }
    }
}

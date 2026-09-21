using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    private const string DatabasePrivateMarker = "private-database-value-4197";

    [Fact]
    public async Task Database_logs_exclude_content_during_live_index_retrieval_history_and_evidence()
    {
        var seed = await SeedGraphAsync();
        await ExecuteAsync("UPDATE documents SET original_filename=@text WHERE id=@id", ("text", DatabasePrivateMarker + ".pdf"), ("id", seed.DocumentA));
        await ExecuteAsync("UPDATE extraction_pages SET canonical_text=@text WHERE id=@id", ("text", "Total 100.00 " + DatabasePrivateMarker), ("id", seed.PageA));
        await ExecuteAsync("UPDATE review_actions SET reason=@text WHERE id=@id", ("text", DatabasePrivateMarker), ("id", seed.ActionA));
        await ExecuteAsync("UPDATE review_sessions SET status='Completed',completed_at_utc=@now WHERE id=@id", ("now", DateTimeOffset.UtcNow), ("id", seed.SessionA));
        using var capture = new DatabaseLogCapture();
        await using var python = await PrivateCitedServer.StartAsync();
        await using var services = CitedServices(seed.OrganizationA, seed.ActorA, python.Url, capture);
        Guid runId;
        Guid indexId;
        await using (var scope = services.CreateAsyncScope())
        {
            AssertDatabaseLoggingDefaults(scope.ServiceProvider.GetRequiredService<AppDbContext>());
            var index = await scope.ServiceProvider.GetRequiredService<IndexReviewedDocumentHandler>()
                .HandleAsync(seed.DocumentA, seed.SessionA, default);
            indexId = index.Id;
            var questions = scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>();
            var question = "total " + DatabasePrivateMarker;
            var result = await questions.HandleAsync(question, DatabasePrivateMarker, [index.Id], 5, default);
            runId = result.Retrieval.Id;
            Assert.Equal("EvidenceReady", result.Stage);
            Assert.Contains(result.Retrieval.Hits, x => x.Content.Contains(DatabasePrivateMarker));
            Assert.All(result.Retrieval.Hits, x => Assert.Contains(DatabasePrivateMarker, x.Filename));
            var replay = await questions.HandleAsync(question, DatabasePrivateMarker, [index.Id], 5, default);
            Assert.True(replay.Retrieval.Replayed);
            Assert.Equal(runId, replay.Retrieval.Id);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<CitedQuestionHandler>().GetAsync(runId, default);
            Assert.Contains(DatabasePrivateMarker, saved.Question);
            var workspace = scope.ServiceProvider.GetRequiredService<CitedWorkspaceHandler>();
            Assert.Contains(DatabasePrivateMarker, Assert.Single((await workspace.SourcesAsync(0, default)).Sources).Filename);
            var pageHit = Assert.Single(saved.Retrieval.Hits.Where(x => x.Kind == "PageText"));
            var opened = await workspace.EvidenceAsync(runId, pageHit.HitId, pageHit.Spans.Single().Id, default);
            Assert.Contains(DatabasePrivateMarker, opened.Page!.Quote);
            var factHit = Assert.Single(saved.Retrieval.Hits.Where(x => x.Kind == "ReviewedFact"));
            var fact = await workspace.EvidenceAsync(runId, factHit.HitId, null, default);
            Assert.Equal(DatabasePrivateMarker, fact.Review!.Reason);
            Assert.Equal(indexId, fact.IndexId);
        }
        Assert.Contains(capture.Entries, x => x.Category.StartsWith("Microsoft.EntityFrameworkCore") && x.EventName == "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted");
        AssertDatabaseLogsPrivate(capture);
    }

    [Fact]
    public async Task Database_constraint_conflict_keeps_private_request_key_out_of_application_logs()
    {
        var graph = await SeedCitedGraphAsync();
        using var capture = new DatabaseLogCapture();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1", capture);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope = services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICitedQuestionRepository>();
            var index = (await repository.GetIndexAsync(graph.Seed.OrganizationA, graph.Index.Id, default))!;
            var run = new Domain.Model.CitedRetrievalRun(Guid.NewGuid(), graph.Seed.OrganizationA, graph.Seed.ActorA,
                DatabasePrivateMarker, DatabasePrivateMarker, LexicalPlan.CurrentVersion, "{}", [index], DateTimeOffset.UtcNow);
            await repository.AddRunAsync(run, default);
            Assert.Equal(attempt == 0 ? CitedQuestionSaveOutcome.Saved : CitedQuestionSaveOutcome.ConstraintConflict,
                await repository.SaveChangesAsync(default));
        }
        Assert.Contains(capture.Entries, x => x.Level >= LogLevel.Error);
        AssertDatabaseLogsPrivate(capture);
    }

    [Fact]
    public async Task Database_conversion_failure_keeps_primary_error_text_out_of_application_logs()
    {
        var graph = await SeedCitedGraphAsync();
        using var capture = new DatabaseLogCapture();
        await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1", capture);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AssertDatabaseLoggingDefaults(db);
        var privateValue = DatabasePrivateMarker;
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.SqlQuery<int>($"SELECT CAST({privateValue} AS integer) AS \"Value\"").ToArrayAsync());
        Assert.Equal(PostgresErrorCodes.InvalidTextRepresentation, error.SqlState);
        // Npgsql's Detail setting does not redact PostgreSQL's primary error message.
        Assert.Contains(DatabasePrivateMarker, error.MessageText);
        Assert.Contains(capture.Entries, x => x.Level >= LogLevel.Error);
        AssertDatabaseLogsPrivate(capture);
    }

    private static void AssertDatabaseLoggingDefaults(AppDbContext db)
    {
        Assert.False(db.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().IsSensitiveDataLoggingEnabled);
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.False(connection.IncludeErrorDetail);
        Assert.False(connection.LogParameters);
    }

    private static void AssertDatabaseLogsPrivate(DatabaseLogCapture capture)
    {
        Assert.NotEmpty(capture.Entries);
        Assert.DoesNotContain(capture.Entries, x => x.Text.Contains(DatabasePrivateMarker) || x.Text.Contains("test-only-cited-token"));
    }

    private sealed class DatabaseLogCapture : ILoggerProvider
    {
        public sealed record Entry(string Category, string? EventName, LogLevel Level, string Text);
        public ConcurrentQueue<Entry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
        public void Dispose() { }
        private sealed class CaptureLogger(DatabaseLogCapture owner, string category) : ILogger
        {
            public bool IsEnabled(LogLevel level) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull { Record(LogLevel.Trace, null, state); return null; }
            private void Record<TState>(LogLevel level, string? eventName, TState state)
            {
                owner.Entries.Enqueue(new(category, eventName, level, state?.ToString() ?? ""));
                if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                    foreach (var field in fields) owner.Entries.Enqueue(new(category, eventName, level,
                        field.Key + ": " + (field.Value is IEnumerable<string> values ? string.Join(",", values) : field.Value)));
            }
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
            { Record(level, id.Name, state); owner.Entries.Enqueue(new(category, id.Name, level, formatter(state, error) + error)); }
        }
    }
}

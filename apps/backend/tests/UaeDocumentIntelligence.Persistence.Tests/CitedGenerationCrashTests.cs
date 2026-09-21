using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Infrastructure;
using UaeDocumentIntelligence.Infrastructure.Persistence;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    // Allow process setup plus the coordinator's existing 45-second operation bound.
    private static readonly TimeSpan CrashProbeWait = TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData("before-Reserved", "Absent", 0, true)]
    [InlineData("after-Reserved", "Reserved", 0, true)]
    [InlineData("before-Dispatched", "Reserved", 0, true)]
    [InlineData("after-Dispatched", "Dispatched", 0, false)]
    [InlineData("provider-accepted", "Dispatched", 1, false)]
    [InlineData("before-Settled", "Dispatched", 1, false)]
    [InlineData("after-Settled", "Settled", 1, false)]
    public async Task Generation_process_crash_preserves_commits_and_restart_never_redispatches(
        string checkpoint, string durableState, int acceptedCalls, bool canResume)
    {
        var graph = await SeedCitedGraphAsync();
        var runId = await GenerationRun(graph);
        // Test-only durable fake-adapter receipt, not provider billing evidence.
        await ExecuteAsync("CREATE SCHEMA IF NOT EXISTS crash_probe");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS crash_probe.calls (operation_id uuid NOT NULL)");
        var probe = new CrashProbe(_database.TestConnectionString, graph.Seed.OrganizationA,
            graph.Seed.ActorA, runId, SpendCommand().Rate, checkpoint);
        using (var child = StartCrashProbe(probe))
        {
            try
            {
                Assert.Equal("checkpoint:" + checkpoint,
                    await child.StandardOutput.ReadLineAsync().WaitAsync(CrashProbeWait));
                Assert.False(child.HasExited);
                child.Kill();
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally { await StopCrashProbe(child); }
        }

        Assert.Equal(durableState, await CrashState(runId));
        Assert.Equal(acceptedCalls, await CrashCalls(runId));
        var events = durableState switch { "Absent" => 0, "Reserved" => 1, "Dispatched" => 2, _ => 3 };
        Assert.Equal(events, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(durableState == "Absent" ? 0 : 1,
            await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts WHERE kind='Preparation'"));
        Assert.Equal(durableState == "Settled" ? 1 : 0,
            await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts WHERE kind='Outcome'"));
        Assert.Equal(durableState == "Settled" ? .000025m : 0m,
            await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));

        // A new OS process uses exactly the same operation, rate snapshot and request bytes.
        Assert.Equal(canResume || durableState == "Settled" ? "completed-unpublished" : "denied:generation_already_dispatched",
            await RunCrashProbe(probe with { Checkpoint = "none" }));
        Assert.Equal(canResume ? 1 : acceptedCalls, await CrashCalls(runId));
        Assert.Equal(canResume ? "Settled" : durableState, await CrashState(runId));
        Assert.Equal(canResume ? 3 : events, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts WHERE kind='Preparation'"));
        Assert.Equal(canResume || durableState == "Settled" ? 1 : 0,
            await ScalarAsync<long>("SELECT count(*) FROM cited_generation_artifacts WHERE kind='Outcome'"));
        Assert.Equal(canResume || durableState == "Settled" ? .000025m : 0m,
            await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(0, await ScalarAsync<long>("SELECT count(*) FROM cited_answers WHERE retrieval_run_id=@id", ("id", runId)));

        // Restart does not bypass tenant isolation, release a dispatched hold or reset the ledger.
        Assert.Equal("denied:retrieval_not_found", await RunCrashProbe(probe with
        { OrganizationId = graph.Seed.OrganizationB, ActorId = graph.Seed.ActorB, Checkpoint = "none" }));
        if (await CrashState(runId) == "Dispatched")
        {
            await using var services = CitedServices(graph.Seed.OrganizationA, graph.Seed.ActorA, "http://127.0.0.1:1");
            var ledger = services.GetRequiredService<ICitedGenerationLedger>();
            var id = await ScalarAsync<Guid>("SELECT id FROM cited_spend_reservations WHERE operation_id=@id", ("id", runId));
            await SpendDenied("invalid_spend_transition", () => ledger.CancelBeforeDispatchAsync(id, default));
            await SpendDenied("budget_reconciliation_required", () => ledger.ReserveAsync(SpendCommand(), true, default));
            if (acceptedCalls == 1)
            {
                // Simulate trusted, independently known synthetic usage. No auto-recovery endpoint.
                await ledger.SettleAsync(id, .000025m, "test-crash-receipt", default);
                await ledger.SettleAsync(id, .000025m, "test-crash-receipt", default);
                Assert.Equal(.000025m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
                Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
                Assert.Equal("denied:generation_already_dispatched", await RunCrashProbe(probe with { Checkpoint = "none" }));
                Assert.Equal(1, await CrashCalls(runId));
            }
        }
        // The receipt schema must not contaminate the application's exact schema contract.
        await Expected_schema_constraints_and_indexes_exist();
    }

    private Task<string> CrashState(Guid runId) => ScalarAsync<string>(
        "SELECT COALESCE((SELECT state FROM cited_spend_reservations WHERE operation_id=@id), 'Absent')", ("id", runId));
    private Task<long> CrashCalls(Guid runId) => ScalarAsync<long>(
        "SELECT count(*) FROM crash_probe.calls WHERE operation_id=@id", ("id", runId));

    private sealed record CrashProbe(string Connection, Guid OrganizationId, Guid ActorId,
        Guid RunId, CitedGenerationRate Rate, string Checkpoint,
        ExecuteCitedRecovery? RecoveryCommand = null, CitedRecoveryEvidence? RecoveryEvidence = null);

    private static Process StartCrashProbe(CrashProbe probe)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(PostgresPersistenceTests).Assembly.Location);
        start.ArgumentList.Add("--cited-crash-probe");
        start.Environment["CITED_TEST_CRASH_PROBE"] = JsonSerializer.Serialize(probe);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start test crash probe.");
    }

    private static async Task<string> RunCrashProbe(CrashProbe probe)
    {
        using var child = StartCrashProbe(probe);
        try
        {
            var output = child.StandardOutput.ReadToEndAsync();
            var errors = child.StandardError.ReadToEndAsync();
            await child.WaitForExitAsync().WaitAsync(CrashProbeWait);
            Assert.Equal(0, child.ExitCode);
            Assert.Equal("", await errors);
            return (await output).Trim();
        }
        finally { await StopCrashProbe(child); }
    }

    private static async Task StopCrashProbe(Process child)
    {
        // This probe runs only managed fake-provider/database code and starts no
        // descendants. Terminate that exact process without enumerating unrelated
        // Windows processes, then enforce the bounded exit wait.
        if (!child.HasExited) child.Kill();
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    // Only the test executable exposes this entry point. It cannot migrate, reset or call a live provider.
    public static async Task<int> Main(string[] args)
    {
        if (args is not ["--cited-crash-probe"]) return 2;
        try
        {
            var probe = JsonSerializer.Deserialize<CrashProbe>(Environment.GetEnvironmentVariable("CITED_TEST_CRASH_PROBE")!)!;
            var connection = new NpgsqlConnectionStringBuilder(probe.Connection);
            if (connection.Database != PostgresDatabaseFixture.TestDatabaseName || connection.Host != "127.0.0.1" ||
                probe.Rate.Provider != "test-only") return 2;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = connection.ConnectionString }).Build();
            var collection = new ServiceCollection().AddInfrastructure(config);
            collection.AddSingleton<IExecutionContext>(new CitedExecution(probe.OrganizationId, probe.ActorId));
            collection.RemoveAll<DbContextOptions<AppDbContext>>();
            collection.AddScoped(_ => new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connection.ConnectionString)
                .AddInterceptors(new CrashCommitInterceptor(probe)).Options);
            if (probe.RecoveryCommand is not null)
            {
                collection.AddSingleton<ICitedRecoveryExecutionAuthorization>(new ExecutionAuthorization());
                if (probe.RecoveryEvidence is not null)
                    collection.AddSingleton<ICitedRecoveryEvidenceVerifier>(new RecoveryTestVerifier(probe.RecoveryEvidence));
            }
            await using var services = collection.BuildServiceProvider();
            // Initialize EF and the guarded test connection before the operation's
            // own deadline starts. Setup performs no writes; production deadlines
            // and the checkpoint/state assertions remain unchanged.
            await using (var warmup = new AppDbContext(services.GetRequiredService<DbContextOptions<AppDbContext>>()))
            {
                _ = warmup.Model;
                using var setupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await warmup.Database.OpenConnectionAsync(setupDeadline.Token);
                await warmup.Database.CloseConnectionAsync();
            }
            var client = new AnswerReply(async (reply, ct) =>
            {
                await using var receiptConnection = new NpgsqlConnection(connection.ConnectionString);
                await receiptConnection.OpenAsync(ct);
                await using var receipt = new NpgsqlCommand("INSERT INTO crash_probe.calls VALUES (@id)", receiptConnection);
                receipt.Parameters.AddWithValue("id", probe.RunId);
                await receipt.ExecuteNonQueryAsync(ct);
                await PauseAt(probe, "provider-accepted");
                return reply;
            });
            try
            {
                if (probe.RecoveryCommand is not null)
                {
                    var recovered = await services.GetRequiredService<ICitedRecoveryExecutor>().ExecuteAsync(probe.RecoveryCommand, default);
                    Console.WriteLine(recovered.Replayed ? "recovery-replayed" : "recovery-committed");
                    return 0;
                }
                var result = await Coordinator(services, client, new(true, probe.Rate)).ProposeAsync(probe.RunId, default);
                if (result.PublicationAllowed) return 3;
                Console.WriteLine("completed-unpublished");
            }
            catch (CitedWorkflowException e) { Console.WriteLine("denied:" + e.Code); }
            return 0;
        }
        catch { Console.Error.WriteLine("Crash probe failed."); return 1; }
    }

    private static async Task PauseAt(CrashProbe probe, string checkpoint)
    {
        if (probe.Checkpoint != checkpoint) return;
        Console.WriteLine("checkpoint:" + checkpoint);
        await Console.Out.FlushAsync();
        // Deliberately no cancellation/cleanup: the parent kills this exact process.
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }

    private sealed class CrashCommitInterceptor(CrashProbe probe) : DbTransactionInterceptor
    {
        private string? _state;
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT state FROM cited_spend_reservations WHERE operation_id=@id";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = probe.RunId;
            command.Parameters.Add(parameter);
            _state = await command.ExecuteScalarAsync(cancellationToken) as string;
            await PauseAt(probe, "before-" + _state);
            return result;
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => PauseAt(probe, "after-" + _state);
    }
}

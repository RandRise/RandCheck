using Microsoft.Extensions.DependencyInjection;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Persistence.Tests;

public sealed partial class PostgresPersistenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Recovery_process_crash_keeps_accounting_audit_and_replay_atomic(bool settle, bool committed)
    {
        var seed = await SeedGraphAsync();
        await using var services = CitedPilotServices(seed.OrganizationA, seed.ActorA, "http://127.0.0.1:1");
        var ledger = services.GetRequiredService<ICitedGenerationLedger>();
        var spend = SpendCommand();
        var reservation = await ledger.ReserveAsync(spend, true, default);
        if (settle) Assert.True(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        var command = settle ? SettleRecovery(reservation.Id) : CancelRecovery(reservation.Id);
        var snapshot = (await services.GetRequiredService<ICitedRecoveryReader>()
            .ReadAsync(seed.OrganizationA, seed.ActorA, reservation.Id, default))!;
        var proof = settle ? await new ExecutionVerifier(.001m).VerifyAsync(snapshot, command.EvidenceReference!, default) : null;
        var finalState = settle ? "Settled" : "Cancelled";
        var checkpoint = (committed ? "after-" : "before-") + finalState;
        var probe = new CrashProbe(_database.TestConnectionString, seed.OrganizationA, seed.ActorA,
            spend.OperationId, spend.Rate, checkpoint, command, proof);
        using (var child = StartCrashProbe(probe))
        {
            try
            {
                Assert.Equal("checkpoint:" + checkpoint, await child.StandardOutput.ReadLineAsync().WaitAsync(CrashProbeWait));
                Assert.False(child.HasExited);
                child.Kill();
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally { await StopCrashProbe(child); }
        }
        Assert.Equal(committed ? finalState : settle ? "Dispatched" : "Reserved",
            await ScalarAsync<string>("SELECT state FROM cited_spend_reservations"));
        Assert.Equal(committed ? 1 : 0, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
        Assert.Equal(settle && committed ? .001m : 0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal((settle ? 2 : 1) + (committed ? 1 : 0), await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        var restart = probe with { Checkpoint = "none" };
        Assert.Equal(committed ? "recovery-replayed" : "recovery-committed", await RunCrashProbe(restart));
        Assert.Equal("recovery-replayed", await RunCrashProbe(restart));
        Assert.Equal(1, await ScalarAsync<long>("SELECT count(*) FROM cited_recovery_audits"));
        Assert.Equal(settle ? .001m : 0m, await ScalarAsync<decimal>("SELECT settled_usd FROM cited_pilot_budgets"));
        Assert.Equal(settle ? 3 : 2, await ScalarAsync<long>("SELECT count(*) FROM cited_spend_events"));
        Assert.False(await ledger.TryMarkDispatchedAsync(reservation.Id, true, default));
        await Expected_schema_constraints_and_indexes_exist();
    }
}

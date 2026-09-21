using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedRecoveryExecutor(DbContextOptions<AppDbContext> options,
    IExecutionContext execution, ICitedRecoveryExecutionAuthorization authorization,
    ICitedRecoveryEvidenceVerifier verifier) : ICitedRecoveryExecutor
{
    public async Task<CitedRecoveryResult> ExecuteAsync(ExecuteCitedRecovery command, CancellationToken ct)
    {
        var organizationId = execution.OrganizationId;
        var operatorId = execution.ActorId;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var token = deadline.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            await Authorize(organizationId, operatorId, token); // Before any storage or proof lookup.
            if (command is null || command.RequestId == Guid.Empty || command.ReservationId == Guid.Empty ||
                command.ExpectedEventSequence < 1 || command.ExpectedState is not
                    (CitedSpendState.Reserved or CitedSpendState.Dispatched or CitedSpendState.Uncertain) ||
                !Enum.IsDefined(command.Action) ||
                (command.Action == CitedRecoveryAction.CancelReserved
                    ? command.ExpectedState != CitedSpendState.Reserved || command.EvidenceReference is not null
                    : command.ExpectedState == CitedSpendState.Reserved || !Reference(command.EvidenceReference)))
                throw Error("invalid_recovery_request", 400);
            var fingerprint = Hash(new { version = "cited-recovery-execution-v1", command });
            await using var db = new AppDbContext(options);
            if (!db.Database.IsNpgsql()) throw Error("ledger_requires_postgresql", 503);
            await using var tx = await db.Database.BeginTransactionAsync(token);
            var budget = await db.CitedPilotBudgets.FromSqlRaw("SELECT * FROM cited_pilot_budgets WHERE id = 1 FOR UPDATE")
                .AsNoTracking().SingleAsync(token);
            // Hold the active operator row through commit; never impersonate the original actor.
            var actor = await db.Actors.FromSqlInterpolated($"SELECT * FROM actors WHERE organization_id = {organizationId} AND id = {operatorId} AND is_active FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(token);
            if (actor is null) throw Error("recovery_not_found", 404);
            await Authorize(organizationId, operatorId, token);
            var prior = await db.CitedRecoveryAudits.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.RequestId == command.RequestId, token);
            if (prior is not null)
            {
                if (prior.CommandSha256 != fingerprint) throw Error("recovery_key_conflict");
                // Historical outcome, not a new mutation: no second evidence use or charge.
                await tx.CommitAsync(token);
                return Result(prior, true);
            }
            var reservation = await db.CitedSpendReservations.AsNoTracking()
                .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == command.ReservationId, token)
                ?? throw Error("recovery_not_found", 404);
            if (reservation.Provider != "test-only") throw Error("recovery_live_not_ready");
            var sequence = await db.CitedSpendEvents.Where(x => x.OrganizationId == organizationId && x.ReservationId == reservation.Id)
                .MaxAsync(x => (int?)x.Sequence, token) ?? 0;
            if (reservation.State != command.ExpectedState || sequence != command.ExpectedEventSequence)
                throw Error("recovery_stale");
            var snapshot = new CitedRecoverySnapshot(reservation.Id, organizationId, reservation.ActorId,
                reservation.OperationId, reservation.RequestSha256, reservation.Provider, reservation.Model,
                reservation.ModelVersion, reservation.State, sequence, reservation.ReservedUsd, reservation.ActualUsd, reservation.CreatedAtUtc);
            var evidence = command.EvidenceReference is null ? null : await Dependency(() =>
                verifier.VerifyAsync(snapshot, command.EvidenceReference, token), token);
            if (command.EvidenceReference is not null && (evidence is null || evidence.Reference != command.EvidenceReference))
                throw Error("recovery_evidence_unavailable");
            await Authorize(organizationId, operatorId, token);
            var now = await db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(token);
            var assessment = CitedRecoveryPolicy.Assess(snapshot, operatorId, evidence, now);
            var expected = command.Action == CitedRecoveryAction.CancelReserved
                ? CitedRecoveryRecommendation.CancelReserved : CitedRecoveryRecommendation.RecordVerifiedCost;
            if (assessment.Recommendation != expected) throw Error("recovery_evidence_unavailable");
            if (evidence is not null && await db.CitedRecoveryAudits.AnyAsync(x =>
                    x.EvidenceId == evidence.EvidenceId || x.EvidenceReference == evidence.Reference, token))
                throw Error("recovery_evidence_conflict");
            var state = command.Action == CitedRecoveryAction.CancelReserved ? CitedSpendState.Cancelled : CitedSpendState.Settled;
            var amount = assessment.VerifiedActualUsd;
            if (amount is not null)
            {
                var settled = checked(budget.SettledUsd + amount.Value);
                await db.CitedPilotBudgets.Where(x => x.Id == 1).ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.SettledUsd, settled)
                    .SetProperty(x => x.RequiresReconciliation, budget.RequiresReconciliation || assessment.RequiresOverrunFreeze), token);
            }
            var changed = await db.CitedSpendReservations.Where(x => x.OrganizationId == organizationId &&
                    x.Id == reservation.Id && x.State == command.ExpectedState)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.State, state)
                    .SetProperty(x => x.ActualUsd, amount).SetProperty(x => x.CompletedAtUtc, now), token);
            if (changed != 1) throw Error("recovery_stale");
            var spendEvent = new CitedSpendEvent(organizationId, reservation.Id, operatorId,
                checked(sequence + 1), state, amount, evidence?.Reference, now);
            var audit = new CitedRecoveryAudit(organizationId, command.RequestId, reservation.Id, reservation.ActorId,
                operatorId, spendEvent.Id, fingerprint, command.Action, reservation.State, state, sequence, amount,
                evidence?.EvidenceId, evidence?.Reference, evidence is null ? null : Hash(evidence), now);
            db.CitedSpendEvents.Add(spendEvent);
            db.CitedRecoveryAudits.Add(audit);
            await db.SaveChangesAsync(token);
            await tx.CommitAsync(token);
            return Result(audit, false); // Never return success before commit. Never automatically retry.
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (CitedWorkflowException) { throw; }
        catch { throw Error("recovery_store_unavailable", 503); } // Includes unknown commit outcome; retain request ID.
    }

    private async Task Authorize(Guid organizationId, Guid operatorId, CancellationToken ct)
    {
        if (organizationId == Guid.Empty || operatorId == Guid.Empty ||
            !await Dependency(() => authorization.CanExecuteAsync(organizationId, operatorId, ct), ct))
            throw Error("recovery_forbidden", 403);
    }
    private static async Task<T> Dependency<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try { return await call().WaitAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch { throw Error("recovery_dependency_unavailable", 503); }
    }
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
    private static bool Reference(string? value) => value is { Length: >= 1 and <= 100 } &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or ':');
    private static CitedRecoveryResult Result(CitedRecoveryAudit audit, bool replayed) =>
        new(audit.Id, audit.ReservationId, audit.OperatorActorId, audit.AfterState, audit.AfterEventSequence, audit.ActualUsd, replayed);
    private static CitedWorkflowException Error(string code, int status = 409) => new(code, "Recovery execution could not be completed.", status);
}

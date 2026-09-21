using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class CitedGenerationLedger(DbContextOptions<AppDbContext> options,
    IExecutionContext execution) : ICitedGenerationLedger, ICitedGenerationJournal, ICitedPilotUnknownHold
{
    private static readonly Guid PilotQuoteId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Task ValidateCallerAsync(CancellationToken ct) => Locked((_, _, _) => Task.FromResult(true), ct);

    public Task<CitedSpendView> ReserveAsync(ReserveCitedGeneration command, bool enabled, CancellationToken ct) =>
        ReserveCore(command, null, enabled, ct);

    private Task<CitedSpendView> ReserveCore(ReserveCitedGeneration command, byte[]? preparation, bool enabled, CancellationToken ct)
    {
        if (!enabled) throw Error("generation_disabled");
        if (command is null || command.OperationId == Guid.Empty || command.Rate is null ||
            command.RequestSha256 is null || command.RequestSha256.Length != 64 ||
            command.RequestSha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw Error("invalid_reservation_request");
        if (!Enum.IsDefined(command.Workload)) throw Error("invalid_reservation_request");
        if (command.Workload == CitedSpendWorkload.Embedding && command.MaximumOutputTokens != 0)
            throw Error("embedding_token_limit");
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command))).ToLowerInvariant();
        return Locked(async (db, budget, now) =>
        {
            var existing = await Owned(db).SingleOrDefaultAsync(x => x.OperationId == command.OperationId, ct);
            if (existing is not null)
            {
                if (existing.Fingerprint != fingerprint || existing.Workload != command.Workload)
                    throw Error("reservation_key_conflict");
                if (preparation is not null)
                {
                    var artifact = await db.CitedGenerationArtifacts.AsNoTracking().SingleOrDefaultAsync(x =>
                        x.OrganizationId == execution.OrganizationId && x.ActorId == execution.ActorId &&
                        x.ReservationId == existing.Id && x.Kind == CitedGenerationArtifactKind.Preparation, ct);
                    if (artifact is null || artifact.PayloadSha256 != command.RequestSha256 ||
                        !artifact.PayloadUtf8.AsSpan().SequenceEqual(preparation)) throw Error("reservation_key_conflict");
                }
                return View(existing, true); // Includes terminal states: never revive a replay.
            }
            var pending = await db.CitedSpendReservations.Where(x => x.State == CitedSpendState.Reserved ||
                x.State == CitedSpendState.Dispatched || x.State == CitedSpendState.Uncertain)
                .SumAsync(x => (decimal?)x.ReservedUsd, ct) ?? 0m;
            var snapshot = new CitedBudgetSnapshot(PilotQuoteId, "USD", budget.CeilingUsd, budget.SettledUsd,
                pending, 0, budget.RequiresReconciliation,
                await db.CitedSpendReservations.Where(x => x.State == CitedSpendState.HeldUnknown)
                    .SumAsync(x => (decimal?)x.ReservedUsd, ct) ?? 0m);
            var quote = command.Workload == CitedSpendWorkload.Embedding
                ? CitedGenerationBudgetPolicy.QuoteEmbedding(command.Rate, snapshot, now, command.MaximumInputTokens, true)
                : CitedGenerationBudgetPolicy.Quote(command.Rate, snapshot, now,
                    command.MaximumInputTokens, command.MaximumOutputTokens, true);
            var reservation = new CitedSpendReservation(Guid.NewGuid(), execution.OrganizationId, execution.ActorId,
                command.OperationId, fingerprint, command.RequestSha256, quote.Provider, quote.Model,
                quote.ModelVersion, quote.RateVersion, JsonSerializer.Serialize(command.Rate), command.Rate.ValidUntilUtc,
                quote.MaximumInputTokens, quote.MaximumOutputTokens, quote.ReservedUsd, now, command.Workload);
            db.CitedSpendReservations.Add(reservation);
            db.CitedSpendEvents.Add(new(execution.OrganizationId, reservation.Id, execution.ActorId,
                1, CitedSpendState.Reserved, null, null, now));
            if (preparation is not null) db.CitedGenerationArtifacts.Add(new(execution.OrganizationId,
                execution.ActorId, reservation.Id, CitedGenerationArtifactKind.Preparation,
                preparation, command.RequestSha256, fingerprint, now));
            await db.SaveChangesAsync(ct);
            return View(reservation, false);
        }, ct);
    }

    public Task<CitedSpendView> GetAsync(Guid reservationId, CancellationToken ct) =>
        Locked(async (db, _, _) => View(await Find(db, reservationId, ct), false), ct);

    public Task<bool> TryMarkDispatchedAsync(Guid reservationId, bool enabled, CancellationToken ct)
    {
        if (!enabled) throw Error("generation_disabled");
        return Locked(async (db, budget, now) =>
        {
            var reservation = await Find(db, reservationId, ct);
            if (reservation.State != CitedSpendState.Reserved) return false;
            if (budget.RequiresReconciliation) throw Error("budget_reconciliation_required");
            if (now >= reservation.RateValidUntilUtc) throw Error("rate_or_model_expired");
            await Transition(db, reservation, CitedSpendState.Dispatched, null, null, now, ct);
            return true; // Returned only after Locked commits. Never retries automatically.
        }, ct);
    }

    public Task MarkUncertainAsync(Guid reservationId, CancellationToken ct) =>
        Locked(async (db, _, now) =>
        {
            var reservation = await Find(db, reservationId, ct);
            if (reservation.State == CitedSpendState.Uncertain) return false;
            if (reservation.State != CitedSpendState.Dispatched) throw Error("invalid_spend_transition");
            await Transition(db, reservation, CitedSpendState.Uncertain, null, null, now, ct);
            return true;
        }, ct);

    public Task CancelBeforeDispatchAsync(Guid reservationId, CancellationToken ct) =>
        Locked(async (db, _, now) =>
        {
            var reservation = await Find(db, reservationId, ct);
            if (reservation.State == CitedSpendState.Cancelled) return false;
            if (reservation.State != CitedSpendState.Reserved) throw Error("invalid_spend_transition");
            await Transition(db, reservation, CitedSpendState.Cancelled, null, null, now, ct);
            return true;
        }, ct);

    public Task SettleAsync(Guid reservationId, decimal actualUsd, string evidenceReference, CancellationToken ct)
    {
        // Exact microdollars: reject silent DB rounding and impossible stored amounts.
        if (actualUsd is < 0 or > 999_999_999_999.999999m || decimal.Round(actualUsd, 6) != actualUsd ||
            string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference.Length > 100 ||
            evidenceReference != evidenceReference.Trim() || evidenceReference.Any(char.IsControl))
            throw Error("invalid_settlement");
        return Locked(async (db, budget, now) =>
        {
            var reservation = await Find(db, reservationId, ct);
            if (reservation.State == CitedSpendState.Settled)
            {
                var prior = await db.CitedSpendEvents.SingleAsync(x => x.ReservationId == reservationId &&
                    x.State == CitedSpendState.Settled, ct);
                if (reservation.ActualUsd != actualUsd || prior.EvidenceReference != evidenceReference)
                    throw Error("settlement_conflict");
                return false;
            }
            if (reservation.State is not (CitedSpendState.Dispatched or CitedSpendState.Uncertain))
                throw Error("invalid_spend_transition");
            // Record reality even if it exceeds the quote; freeze further admission.
            await db.CitedPilotBudgets.Where(x => x.Id == 1).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.SettledUsd, budget.SettledUsd + actualUsd)
                .SetProperty(x => x.RequiresReconciliation, budget.RequiresReconciliation || actualUsd > reservation.ReservedUsd), ct);
            await Transition(db, reservation, CitedSpendState.Settled, actualUsd, evidenceReference, now, ct);
            return true;
        }, ct);
    }

    private IQueryable<CitedSpendReservation> Owned(AppDbContext db) => db.CitedSpendReservations.AsNoTracking()
        .Where(x => x.OrganizationId == execution.OrganizationId && x.ActorId == execution.ActorId);

    private async Task<CitedSpendReservation> Find(AppDbContext db, Guid id, CancellationToken ct) =>
        await Owned(db).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Error("reservation_not_found", 404);

    private async Task Transition(AppDbContext db, CitedSpendReservation reservation, CitedSpendState state,
        decimal? actualUsd, string? evidenceReference, DateTimeOffset now, CancellationToken ct)
    {
        DateTimeOffset? completed = state is CitedSpendState.Settled or CitedSpendState.Cancelled or CitedSpendState.HeldUnknown ? now : null;
        var changed = await Owned(db).Where(x => x.Id == reservation.Id && x.State == reservation.State)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.State, state)
                .SetProperty(x => x.ActualUsd, actualUsd).SetProperty(x => x.CompletedAtUtc, completed), ct);
        if (changed != 1) throw Error("spend_conflict");
        var sequence = await db.CitedSpendEvents.Where(x => x.ReservationId == reservation.Id).MaxAsync(x => x.Sequence, ct);
        db.CitedSpendEvents.Add(new(execution.OrganizationId, reservation.Id, execution.ActorId,
            sequence + 1, state, actualUsd, evidenceReference, now));
        await db.SaveChangesAsync(ct);
    }

    private async Task<T> Locked<T>(Func<AppDbContext, CitedPilotBudget, DateTimeOffset, Task<T>> action, CancellationToken ct)
    {
        // Fresh context per call avoids stale tracked balances and unrelated SaveChanges.
        await using var db = new AppDbContext(options);
        if (!db.Database.IsNpgsql()) throw Error("ledger_requires_postgresql", 503);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var budget = await db.CitedPilotBudgets.FromSqlRaw("SELECT * FROM cited_pilot_budgets WHERE id = 1 FOR UPDATE")
                .AsNoTracking().SingleAsync(ct);
            var actor = await db.Actors.FromSqlInterpolated($"SELECT * FROM actors WHERE organization_id = {execution.OrganizationId} AND id = {execution.ActorId} AND is_active FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (actor is null) throw Error("reservation_not_found", 404);
            var now = await db.Database.SqlQueryRaw<DateTimeOffset>("SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
            var result = await action(db, budget, now);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or InvalidOperationException or OverflowException or JsonException)
        {
            // Unknown commit outcomes must be resolved by replay/read, never blind retry.
            throw Error("spend_store_unavailable", 503);
        }
    }

    private static CitedSpendView View(CitedSpendReservation r, bool replayed) =>
        new(r.Id, r.OperationId, r.State, r.ReservedUsd, r.ActualUsd, replayed, r.Workload);
    private static CitedWorkflowException Error(string code, int status = 409) =>
        new(code, "The generation spending operation could not be completed.", status);
}

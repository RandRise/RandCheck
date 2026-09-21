using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class CitedGenerationLedger
{
    public Task<CitedSpendView> RetainAsync(Guid reservationId, bool enabled, CancellationToken ct)
        => RetainCore(reservationId, enabled, false, ct);

    public Task<CitedSpendView> RetainLowPreflightFailureAsync(Guid reservationId, bool enabled, CancellationToken ct)
        => RetainCore(reservationId, enabled, true, ct);

    public Task<CitedSpendView> RetainSourceGenerationFailureAsync(Guid reservationId, bool enabled, CancellationToken ct)
        => RetainCore(reservationId, enabled, true, ct, sourceFailure: true);

    public Task<CitedSpendView> RetainSourceCountFailureAsync(Guid reservationId, bool enabled, CancellationToken ct)
        => RetainCore(reservationId, enabled, true, ct, countFailure: true);

    private Task<CitedSpendView> RetainCore(Guid reservationId, bool enabled, bool lowPreflight, CancellationToken ct,
        bool sourceFailure = false, bool countFailure = false)
    {
        if (!enabled) throw Error("pilot_unknown_hold_disabled");
        return Locked(async (db, budget, now) =>
        {
            var reservation = await Find(db, reservationId, ct);
            var connectionRecovery = sourceFailure && reservation.OperationId == Guid.Parse("7351e37b-3261-48aa-8294-71c3c521710f");
            if (reservation.State == CitedSpendState.HeldUnknown) return View(reservation, true);
            if (reservation.State != CitedSpendState.Uncertain || reservation.ActualUsd is not null ||
                reservation.Provider != "openai" || (lowPreflight ?
                reservation.Workload != CitedSpendWorkload.Generation || reservation.Model != "gpt-5-nano" ||
                reservation.ModelVersion != CitedOpenAiPilot.Rate.ModelVersion || reservation.ReservedUsd > .002m ||
                reservation.MaximumOutputTokens != 4000 :
                reservation.Workload != CitedSpendWorkload.Embedding ||
                reservation.Model != CitedEmbeddingStorage.Model || reservation.ModelVersion != CitedEmbeddingStorage.Model ||
                reservation.ReservedUsd > .000160m || reservation.MaximumOutputTokens != 0))
                throw Error("pilot_unknown_hold_ineligible");
            var records = await db.CitedGenerationArtifacts.AsNoTracking()
                .Where(x => x.ReservationId == reservationId).ToArrayAsync(ct);
            var preparation = records.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Preparation);
            var outcome = records.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Outcome);
            if (preparation is null || outcome is null || preparation.PayloadSha256 != reservation.RequestSha256 ||
                records.Any(x => ArtifactHash(x.PayloadUtf8) != x.PayloadSha256))
                throw Error("pilot_unknown_hold_ineligible");
            using var parsed = JsonDocument.Parse(outcome.PayloadUtf8);
            var p = parsed.RootElement;
            if (!p.TryGetProperty("outcome", out var status) || status.GetString() != "transport_uncertain")
                throw Error("pilot_unknown_hold_ineligible");
            if (lowPreflight)
            {
                using var prepared = JsonDocument.Parse(preparation.PayloadUtf8);
                var wire = prepared.RootElement.GetProperty("preparation").GetProperty("openAi");
                if (wire.GetProperty("reasoningEffort").GetString() != "low" ||
                    !p.TryGetProperty("openAi", out var accounting) ||
                    accounting.GetProperty("generationSha256").GetString() != wire.GetProperty("generationSha256").GetString() ||
                    accounting.GetProperty("accountingKnown").ValueKind != JsonValueKind.False ||
                    (sourceFailure ? new[] { "providerRequestId", "responseId", "responseSha256", "responseUtf8", "httpStatus" } :
                    new[] { "countRequestId", "countResponseUtf8", "measuredInputTokens", "providerRequestId",
                        "responseId", "responseSha256", "responseUtf8", "httpStatus" }).Any(k =>
                            !accounting.TryGetProperty(k, out var v) || v.ValueKind != JsonValueKind.Null) ||
                    p.GetProperty("inputTokens").GetInt32() != 0 || p.GetProperty("outputTokens").GetInt32() != 0)
                    throw Error("pilot_unknown_hold_ineligible");
                if (countFailure && (prepared.RootElement.GetProperty("preparation").GetProperty("promptVersion").GetString() != "cited-answer-prompt-v4" ||
                    !accounting.TryGetProperty("transportErrorCode", out var code) || code.GetString() != "openai_transport_uncertain" ||
                    !accounting.TryGetProperty("transportFailureKind", out var kind) || kind.GetString() != "network"))
                    throw Error("pilot_unknown_hold_ineligible");
                if (sourceFailure)
                {
                    var receipt = prepared.RootElement.GetProperty("preparation");
                    var countId = accounting.GetProperty("countRequestId").GetString();
                    var measured = accounting.GetProperty("measuredInputTokens").GetInt32();
                    var countBytes = accounting.GetProperty("countResponseUtf8").GetBytesFromBase64();
                    if (receipt.GetProperty("promptVersion").GetString() != "cited-answer-prompt-v4" ||
                        countId is null || !System.Text.RegularExpressions.Regex.IsMatch(countId, "^req_[A-Za-z0-9_-]{1,196}$") ||
                        measured is < 1 or > 8000 || countBytes.Length is < 1 or > 1024)
                        throw Error("pilot_unknown_hold_ineligible");
                    using var count = JsonDocument.Parse(countBytes);
                    if (count.RootElement.EnumerateObject().Count() != 2 ||
                        count.RootElement.GetProperty("object").GetString() != "response.input_tokens" ||
                        count.RootElement.GetProperty("input_tokens").GetInt32() != measured)
                        throw Error("pilot_unknown_hold_ineligible");
                }
            }
            else if (!p.TryGetProperty("accountingKnown", out var known) || known.ValueKind != JsonValueKind.False ||
                !p.TryGetProperty("inputTokens", out var tokens) || tokens.ValueKind != JsonValueKind.Null)
                throw Error("pilot_unknown_hold_ineligible");

            // Never waive other unresolved dispatches, known overruns, or flagged settled responses.
            if (await db.CitedSpendReservations.AnyAsync(x => x.Id != reservationId &&
                    (x.State == CitedSpendState.Reserved || x.State == CitedSpendState.Dispatched ||
                     x.State == CitedSpendState.Uncertain || x.ActualUsd > x.ReservedUsd), ct))
                throw Error("pilot_other_reconciliation_required");
            var flagged = await (from a in db.CitedGenerationArtifacts
                join r in db.CitedSpendReservations on a.ReservationId equals r.Id
                where r.State == CitedSpendState.Settled && a.Kind == CitedGenerationArtifactKind.Outcome
                select a.PayloadUtf8).ToArrayAsync(ct);
            foreach (var bytes in flagged)
            {
                using var prior = JsonDocument.Parse(bytes);
                if (prior.RootElement.TryGetProperty("requiresReconciliation", out var flag) && flag.ValueKind != JsonValueKind.False)
                    throw Error("pilot_other_reconciliation_required");
            }
            var retained = await db.CitedSpendReservations.Where(x => x.State == CitedSpendState.HeldUnknown)
                .SumAsync(x => (decimal?)x.ReservedUsd, ct) ?? 0m;
            var generationHeld = await db.CitedSpendReservations.Where(x => x.State == CitedSpendState.HeldUnknown &&
                x.Workload == CitedSpendWorkload.Generation).SumAsync(x => (decimal?)x.ReservedUsd, ct) ?? 0m;
            if (countFailure)
            {
                // Only separately evidenced count-stage network failures use this allowance.
                // At most q004 plus the 26 untouched frozen cases, each fully retained.
                var countHeld = await (from r in db.CitedSpendReservations
                    where r.State == CitedSpendState.HeldUnknown && db.CitedSpendEvents.Any(e =>
                        e.ReservationId == r.Id && e.EvidenceReference == "pilot-source-count-hold-v1:authorized-budget")
                    select (decimal?)r.ReservedUsd).SumAsync(ct) ?? 0m;
                var connectionHeld = await db.CitedSpendReservations.Where(x => x.State == CitedSpendState.HeldUnknown &&
                    x.OperationId == Guid.Parse("7351e37b-3261-48aa-8294-71c3c521710f"))
                    .SumAsync(x => (decimal?)x.ReservedUsd, ct) ?? 0m;
                if (connectionHeld > .002m || countHeld + reservation.ReservedUsd > .054m || generationHeld - countHeld - connectionHeld > .004m)
                    throw Error("pilot_unknown_hold_limit");
            }
            if ((lowPreflight ? generationHeld + reservation.ReservedUsd > (connectionRecovery ? .008m : countFailure ? .058m : sourceFailure ? .004m : .002m) :
                retained - generationHeld + reservation.ReservedUsd > .001m) ||
                retained + reservation.ReservedUsd > (connectionRecovery ? .009m : countFailure ? .059m : sourceFailure ? .005m : .003m) ||
                budget.SettledUsd + retained + reservation.ReservedUsd > budget.CeilingUsd)
                throw Error("pilot_unknown_hold_limit");
            // This explicitly accepts pilot uncertainty. It does not verify zero/final cost.
            // The amount remains reserved and included in every subsequent admission calculation.
            await Transition(db, reservation, CitedSpendState.HeldUnknown, null,
                connectionRecovery ? "pilot-source-connection-recovery-v1:authorized-budget" :
                countFailure ? "pilot-source-count-hold-v1:authorized-budget" :
                sourceFailure ? "pilot-source-generation-hold-v1:authorized-budget" :
                lowPreflight ? "pilot-local-preflight-hold-v1:authorized-budget" : "pilot-unknown-hold-v1:user-accepted", now, ct);
            await db.CitedPilotBudgets.Where(x => x.Id == 1).ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.RequiresReconciliation, false), ct);
            return View(await Find(db, reservationId, ct), false);
        }, ct);
    }
}

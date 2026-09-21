using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class CitedGenerationLedger
{
    public Task<CitedSpendView> ReservePreparedAsync(ReserveCitedGeneration command,
        ReadOnlyMemory<byte> preparationUtf8, bool enabled, CancellationToken ct)
    {
        if (!enabled) throw Error("generation_disabled");
        var bytes = ArtifactBytes(preparationUtf8, 98304);
        if (command is null || ArtifactHash(bytes) != command.RequestSha256)
            throw Error("invalid_generation_artifact");
        return ReserveCore(command, bytes, enabled, ct);
    }

    public Task<SavedCitedGeneration?> GetSavedAsync(Guid operationId, CancellationToken ct) =>
        Locked<SavedCitedGeneration?>(async (db, _, _) =>
        {
            var reservation = await Owned(db).SingleOrDefaultAsync(x => x.OperationId == operationId, ct);
            if (reservation is null) return null;
            var records = await db.CitedGenerationArtifacts.AsNoTracking().Where(x =>
                x.OrganizationId == execution.OrganizationId && x.ActorId == execution.ActorId &&
                x.ReservationId == reservation.Id).ToArrayAsync(ct);
            foreach (var record in records)
                if (ArtifactHash(record.PayloadUtf8) != record.PayloadSha256)
                    throw Error("generation_artifact_corrupt", 503);
            var preparation = records.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Preparation);
            if (preparation is not null && preparation.PayloadSha256 != reservation.RequestSha256)
                throw Error("generation_artifact_corrupt", 503);
            return new(View(reservation, true), preparation?.PayloadUtf8.ToArray(),
                records.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Outcome)?.PayloadUtf8.ToArray());
        }, ct);

    public Task RecordOutcomeAsync(Guid reservationId, ReadOnlyMemory<byte> outcomeUtf8,
        decimal? actualUsd, string? evidenceReference, bool requiresReconciliation, CancellationToken ct)
    {
        var bytes = ArtifactBytes(outcomeUtf8, 524288);
        if (actualUsd is not null && (actualUsd is < 0 or > 999_999_999_999.999999m ||
                decimal.Round(actualUsd.Value, 6) != actualUsd.Value || string.IsNullOrWhiteSpace(evidenceReference) ||
                evidenceReference.Length > 100 || evidenceReference != evidenceReference.Trim() || evidenceReference.Any(char.IsControl)) ||
            actualUsd is null && evidenceReference is not null)
            throw Error("invalid_settlement");
        var payloadHash = ArtifactHash(bytes);
        var fingerprint = ArtifactHash(JsonSerializer.SerializeToUtf8Bytes(new
            { version = "cited-generation-outcome-v1", reservationId, payloadHash, actualUsd, evidenceReference, requiresReconciliation }));
        return Locked(async (db, budget, now) =>
        {
            var reservation = await Find(db, reservationId, ct);
            var records = await db.CitedGenerationArtifacts.AsNoTracking().Where(x =>
                x.OrganizationId == execution.OrganizationId && x.ActorId == execution.ActorId &&
                x.ReservationId == reservationId).ToArrayAsync(ct);
            if (!records.Any(x => x.Kind == CitedGenerationArtifactKind.Preparation))
                throw Error("generation_preparation_required");
            var prior = records.SingleOrDefault(x => x.Kind == CitedGenerationArtifactKind.Outcome);
            if (prior is not null)
            {
                if (prior.CommandSha256 != fingerprint || !prior.PayloadUtf8.AsSpan().SequenceEqual(bytes))
                    throw Error("generation_outcome_conflict");
                return false; // Historical result; no second charge, even after separate recovery.
            }
            if (reservation.State is not (CitedSpendState.Dispatched or CitedSpendState.Uncertain))
                throw Error("invalid_spend_transition");
            db.CitedGenerationArtifacts.Add(new(execution.OrganizationId, execution.ActorId, reservationId,
                CitedGenerationArtifactKind.Outcome, bytes, payloadHash, fingerprint, now));
            if (actualUsd is not null)
            {
                var settled = checked(budget.SettledUsd + actualUsd.Value);
                await db.CitedPilotBudgets.Where(x => x.Id == 1).ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.SettledUsd, settled)
                    .SetProperty(x => x.RequiresReconciliation, budget.RequiresReconciliation ||
                        requiresReconciliation || actualUsd > reservation.ReservedUsd), ct);
                await Transition(db, reservation, CitedSpendState.Settled, actualUsd, evidenceReference, now, ct);
            }
            else
            {
                if (requiresReconciliation) await db.CitedPilotBudgets.Where(x => x.Id == 1)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RequiresReconciliation, true), ct);
                if (reservation.State == CitedSpendState.Dispatched)
                    await Transition(db, reservation, CitedSpendState.Uncertain, null, null, now, ct);
                else await db.SaveChangesAsync(ct);
            }
            return true;
        }, ct);
    }

    private static string ArtifactHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] ArtifactBytes(ReadOnlyMemory<byte> input, int limit)
    {
        if (input.Length is < 1 || input.Length > limit) throw Error("invalid_generation_artifact");
        var bytes = input.ToArray(); // Snapshot before waiting for a lock/IO; caller cannot change saved bytes.
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw Error("invalid_generation_artifact");
            Unique(document.RootElement);
            return bytes;
        }
        catch (JsonException) { throw Error("invalid_generation_artifact"); }
    }

    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Error("invalid_generation_artifact");
                Unique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) Unique(child);
    }
}

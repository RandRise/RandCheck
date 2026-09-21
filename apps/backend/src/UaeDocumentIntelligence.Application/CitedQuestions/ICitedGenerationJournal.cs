namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record SavedCitedGeneration(CitedSpendView Spend, byte[]? PreparationUtf8, byte[]? OutcomeUtf8)
{
    public override string ToString() => "Saved generation (content omitted)";
}

/// <summary>Internal canonical records, not a caller-controlled billing or answer publication API.</summary>
public interface ICitedGenerationJournal
{
    // RequestSha256 must bind these exact preparation/dispatch bytes.
    Task<CitedSpendView> ReservePreparedAsync(ReserveCitedGeneration command,
        ReadOnlyMemory<byte> preparationUtf8, bool enabled, CancellationToken ct);
    Task<SavedCitedGeneration?> GetSavedAsync(Guid operationId, CancellationToken ct);
    // Trusted usage only. Unknown usage retains the hold. The artifact and state commit together.
    Task RecordOutcomeAsync(Guid reservationId, ReadOnlyMemory<byte> outcomeUtf8,
        decimal? actualUsd, string? evidenceReference, bool requiresReconciliation, CancellationToken ct);
}

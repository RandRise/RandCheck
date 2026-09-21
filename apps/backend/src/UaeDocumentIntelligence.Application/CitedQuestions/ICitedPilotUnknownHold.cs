namespace UaeDocumentIntelligence.Application.CitedQuestions;

// Private operator action, separate from verified billing recovery. No public route.
// Each method applies its separately documented development-pilot acceptance policy.
public interface ICitedPilotUnknownHold
{
    Task<CitedSpendView> RetainAsync(Guid reservationId, bool enabled, CancellationToken ct);
    Task<CitedSpendView> RetainLowPreflightFailureAsync(Guid reservationId, bool enabled, CancellationToken ct);
    Task<CitedSpendView> RetainSourceCountFailureAsync(Guid reservationId, bool enabled, CancellationToken ct);
    Task<CitedSpendView> RetainSourceGenerationFailureAsync(Guid reservationId, bool enabled, CancellationToken ct);
}

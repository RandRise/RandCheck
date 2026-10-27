namespace UaeDocumentIntelligence.Application.Abstractions;

public enum ReviewSaveOutcome
{
    Saved, ConcurrencyConflict, ConstraintConflict
}

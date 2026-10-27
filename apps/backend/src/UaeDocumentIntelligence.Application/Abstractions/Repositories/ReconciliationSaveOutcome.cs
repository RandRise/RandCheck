namespace UaeDocumentIntelligence.Application.Abstractions;

public enum ReconciliationSaveOutcome
{
    Saved, ConcurrencyConflict, ConstraintConflict
}

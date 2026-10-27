namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record EvaluateReconciliationCaseCommand(decimal? QuantityTolerance = null,
    decimal? UnitPriceTolerance = null, decimal? TaxTolerance = null,
    decimal? LineTotalTolerance = null, decimal? DocumentTotalTolerance = null,
    string? RuleSetVersion = null, string? OrderScope = null);

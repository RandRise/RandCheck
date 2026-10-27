namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationCoverageView(string Status, int CompletedChecks, int Differences,
    int CouldNotCheck, string Conclusion, int PassedChecks = 0, int TotalChecks = 0,
    string Outcome = "ChecksIncomplete");

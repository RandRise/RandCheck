namespace UaeDocumentIntelligence.Application.Abstractions;

public sealed record ReconciliationCaseSummary(Guid Id, string Name, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int DocumentCount, int RunCount,
    string Reference = "", string Category = "Uncategorized", string WorkflowKind = "Legacy");

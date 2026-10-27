namespace UaeDocumentIntelligence.Application.Abstractions;

public sealed record DocumentMetrics(int TotalDocuments, int ReadyDocuments, int FailedDocuments,
    double? AverageProcessingMs, ReviewActionMetrics? Review = null);

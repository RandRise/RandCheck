using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Documents.Queries;

public sealed class MetricsQuery(IDocumentRepository documents, IExecutionContext execution)
{
    public Task<DocumentMetrics> ExecuteAsync(CancellationToken cancellationToken) =>
        documents.GetMetricsAsync(execution.OrganizationId, cancellationToken);
}

using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Documents.Queries;

public sealed class DocumentQueries(IDocumentRepository documents, IExecutionContext execution)
{
    public Task<IReadOnlyList<DocumentWorkItem>> WorkListAsync(CancellationToken cancellationToken) =>
        documents.ListWorkAsync(execution.OrganizationId, cancellationToken);
    public async Task<IReadOnlyList<DocumentView>> ListAsync(CancellationToken cancellationToken) =>
        (await documents.ListAsync(execution.OrganizationId, cancellationToken)).Select(DocumentView.From).ToList();

    public async Task<DocumentView?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(execution.OrganizationId, id, cancellationToken);
        return document is null ? null : DocumentView.From(document);
    }
}

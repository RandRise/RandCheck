using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Application.Documents.Queries;

public sealed class DocumentLibraryQueries(IDocumentLibraryRepository documents, IExecutionContext execution)
{
    public Task<DocumentLibraryPage> SearchAsync(DocumentLibraryQuery query, CancellationToken cancellationToken) =>
        documents.SearchAsync(execution.OrganizationId, query, cancellationToken);
}

using UaeDocumentIntelligence.Application.Documents;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IDocumentLibraryRepository
{
    Task<DocumentLibraryPage> SearchAsync(Guid organizationId, DocumentLibraryQuery query, CancellationToken cancellationToken);
}

using UaeDocumentIntelligence.Application.Documents;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken);

    Task<Document?> GetAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);

    Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken cancellationToken);

    async Task<IReadOnlyList<DocumentWorkItem>> ListWorkAsync(Guid organizationId, CancellationToken cancellationToken) =>
        (await ListAsync(organizationId, cancellationToken)).Select(DocumentWorkItem.From).ToArray();

    Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken);

    Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken cancellationToken);
}

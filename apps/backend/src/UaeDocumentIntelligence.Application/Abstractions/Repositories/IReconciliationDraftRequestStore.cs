using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IReconciliationDraftRequestStore
{
    Task<ReconciliationDraftRequest?> FindAsync(Guid organizationId, Guid actorId,
        string requestKey, CancellationToken cancellationToken);

    Task AddAsync(ReconciliationDraftRequest request, CancellationToken cancellationToken);
}

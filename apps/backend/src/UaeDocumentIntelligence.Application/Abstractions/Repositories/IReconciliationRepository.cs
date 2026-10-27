using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface IReconciliationRepository
{
    Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid organizationId, int offset,
        int limit, CancellationToken cancellationToken, ReconciliationCaseFilter? filter = null);

    Task AddAsync(ReconciliationCase reconciliationCase, CancellationToken cancellationToken);

    Task<ReconciliationCase?> GetAsync(Guid organizationId, Guid reconciliationCaseId,
        CancellationToken cancellationToken);

    Task<ReconciliationCase?> GetByCreationRequestAsync(Guid organizationId, Guid actorId,
        string requestKey, CancellationToken cancellationToken);

    Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

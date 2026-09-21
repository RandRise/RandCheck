using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Abstractions;

public interface ICitedQuestionRepository
{
    Task AddIndexAsync(CitedDocumentIndex index, CancellationToken cancellationToken);
    Task<CitedDocumentIndex?> GetIndexAsync(Guid organizationId, Guid indexId, CancellationToken cancellationToken);
    Task AddRunAsync(CitedRetrievalRun run, CancellationToken cancellationToken);
    Task<CitedRetrievalRun?> GetRunAsync(Guid organizationId, Guid runId, CancellationToken cancellationToken);
    Task<CitedQuestionSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken);
}

public enum CitedQuestionSaveOutcome { Saved, ConcurrencyConflict, ConstraintConflict }

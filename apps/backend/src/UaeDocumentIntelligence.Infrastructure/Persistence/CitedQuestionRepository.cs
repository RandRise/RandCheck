using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedQuestionRepository(AppDbContext db) : ICitedQuestionRepository
{
    public async Task AddIndexAsync(CitedDocumentIndex index, CancellationToken cancellationToken) =>
        await db.CitedDocumentIndexes.AddAsync(index, cancellationToken);

    public Task<CitedDocumentIndex?> GetIndexAsync(Guid organizationId, Guid indexId,
        CancellationToken cancellationToken) => db.CitedDocumentIndexes.AsSplitQuery()
        .Include(x => x.Chunks).ThenInclude(x => x.Spans)
        .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == indexId, cancellationToken);

    public async Task AddRunAsync(CitedRetrievalRun run, CancellationToken cancellationToken) =>
        await db.CitedRetrievalRuns.AddAsync(run, cancellationToken);

    public Task<CitedRetrievalRun?> GetRunAsync(Guid organizationId, Guid runId,
        CancellationToken cancellationToken) => db.CitedRetrievalRuns.AsSplitQuery()
        .Include(x => x.Sources).Include(x => x.Hits)
        .Include(x => x.Answer).ThenInclude(x => x!.Claims).ThenInclude(x => x.Citations)
        .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == runId, cancellationToken);

    public async Task<CitedQuestionSaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return CitedQuestionSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return CitedQuestionSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return CitedQuestionSaveOutcome.ConstraintConflict;
        }
    }
}

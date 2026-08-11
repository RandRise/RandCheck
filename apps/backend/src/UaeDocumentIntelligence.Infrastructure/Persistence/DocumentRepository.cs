using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class DocumentRepository(AppDbContext db) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken cancellationToken) =>
        await db.Documents.AddAsync(document, cancellationToken);

    public Task<Document?> GetAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
        MachineOutputQuery().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == documentId,
            cancellationToken);

    public Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
        ReviewQuery().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == documentId,
            cancellationToken);

    public async Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await ListQuery().Where(x => x.OrganizationId == organizationId).OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
        (await db.ExtractionRuns.Where(x => x.OrganizationId == organizationId && x.DocumentId == documentId)
            .MaxAsync(x => (int?)x.RunNumber, cancellationToken) ?? 0) + 1;

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return ReviewSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return ReviewSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ReviewSaveOutcome.ConstraintConflict;
        }
    }

    public async Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var source = db.Documents.Where(x => x.OrganizationId == organizationId);
        var durations = db.ExtractionRuns.Where(x => x.OrganizationId == organizationId && x.DurationMs != null);
        return new DocumentMetrics(
            await source.CountAsync(cancellationToken),
            await source.CountAsync(x => x.ProcessingStatus == DocumentProcessingStatus.ReadyForReview, cancellationToken),
            await source.CountAsync(x => x.ProcessingStatus == DocumentProcessingStatus.ExtractionFailed, cancellationToken),
            await durations.Select(x => (double?)x.DurationMs).AverageAsync(cancellationToken));
    }

    private IQueryable<Document> ListQuery() => db.Documents
        .AsSplitQuery()
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence);

    private IQueryable<Document> MachineOutputQuery() => db.Documents
        .AsSplitQuery()
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Pages)
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Warnings);

    private IQueryable<Document> ReviewQuery() => db.Documents
        .AsSplitQuery()
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Fields).ThenInclude(x => x.Evidence)
        .Include(x => x.ReviewSessions).ThenInclude(x => x.Actions);
}

using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Application.Documents;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class DocumentRepository(AppDbContext db) : IDocumentRepository, IDocumentLibraryRepository
{
    public async Task<IReadOnlyList<DocumentWorkItem>> ListWorkAsync(Guid organizationId, CancellationToken ct)
        => await ReadWorkItemsAsync(db.Documents.AsNoTracking().Where(d => d.OrganizationId == organizationId), ct);

    private static async Task<IReadOnlyList<DocumentWorkItem>> ReadWorkItemsAsync(IQueryable<Document> documents, CancellationToken ct)
    {
        var rows = await documents
            .OrderByDescending(d => d.CreatedAtUtc).ThenBy(d => d.Id)
            .Select(d => new
            {
                d.Id, d.OriginalFilename, d.SizeBytes, d.ProcessingStatus, d.CreatedAtUtc,
                Selected = d.ExtractionRuns.Where(r => r.Status == ExtractionRunStatus.Succeeded || r.Status == ExtractionRunStatus.Partial)
                    .OrderByDescending(r => r.RunNumber).Select(r => new { r.Id, r.DocumentType, r.Language, r.ModelName, r.ProviderName }).FirstOrDefault(),
                Latest = d.ExtractionRuns.OrderByDescending(r => r.RunNumber)
                    .Select(r => new { r.Status, r.DurationMs, r.SafeFailureSummary }).FirstOrDefault(),
                ActiveRun = d.ReviewSessions.Where(s => s.Status == ReviewStatus.Pending || s.Status == ReviewStatus.InProgress)
                    .OrderByDescending(s => s.RevisionNumber).Select(s => (Guid?)s.ExtractionRunId).FirstOrDefault(),
                CompletedSelected = d.ReviewSessions.Any(s => s.Status == ReviewStatus.Completed && s.ExtractionRunId ==
                    d.ExtractionRuns.Where(r => r.Status == ExtractionRunStatus.Succeeded || r.Status == ExtractionRunStatus.Partial)
                        .OrderByDescending(r => r.RunNumber).Select(r => r.Id).FirstOrDefault()),
                LatestCompleted = d.ReviewSessions.Where(s => s.Status == ReviewStatus.Completed)
                    .OrderByDescending(s => s.RevisionNumber).Select(s => (Guid?)s.Id).FirstOrDefault()
            }).ToListAsync(ct);
        return rows.Select(x => new DocumentWorkItem(x.Id, x.OriginalFilename, x.SizeBytes,
            DocumentWorkItem.ProcessingState(x.ProcessingStatus), x.Selected?.DocumentType, x.Selected?.Language,
            x.Selected?.ModelName ?? x.Selected?.ProviderName, x.Latest?.DurationMs, x.Latest?.SafeFailureSummary,
            x.CreatedAtUtc, x.Selected is null ? null : x.ActiveRun.HasValue ? "InProgress" : x.CompletedSelected ? "Reviewed" : "NeedsReview",
            x.Selected?.Id, x.ActiveRun ?? x.Selected?.Id, x.LatestCompleted, x.ActiveRun.HasValue && x.ActiveRun != x.Selected?.Id,
            x.Latest is null ? null : DocumentWorkItem.ExtractionState(x.Latest.Status))).ToArray();
    }
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
        var sessions = db.ReviewSessions.Where(x => x.OrganizationId == organizationId);
        var actions = db.ReviewActions.Where(x => x.OrganizationId == organizationId);
        var actionCounts = await actions.GroupBy(x => x.Action)
            .Select(group => new { Action = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Action, x => x.Count, cancellationToken);
        var confirmed = actionCounts.GetValueOrDefault(ReviewActionType.Confirmed);
        var corrected = actionCounts.GetValueOrDefault(ReviewActionType.Corrected);
        var added = actionCounts.GetValueOrDefault(ReviewActionType.Added);
        var cleared = actionCounts.GetValueOrDefault(ReviewActionType.Cleared);
        var machineCleared = await actions.CountAsync(x =>
            x.Action == ReviewActionType.Cleared && x.MachineFieldValueId != null,
            cancellationToken);
        var machineDecisions = confirmed + corrected + machineCleared;
        var reviewMetrics = new ReviewActionMetrics(
            await sessions.CountAsync(cancellationToken),
            await sessions.CountAsync(x => x.Status == ReviewStatus.Completed, cancellationToken),
            confirmed + corrected + added + cleared,
            confirmed, corrected, added, cleared,
            machineDecisions == 0 ? null : (double)(corrected + machineCleared) / machineDecisions);
        return new DocumentMetrics(
            await source.CountAsync(cancellationToken),
            await source.CountAsync(x => x.ProcessingStatus == DocumentProcessingStatus.ReadyForReview, cancellationToken),
            await source.CountAsync(x => x.ProcessingStatus == DocumentProcessingStatus.ExtractionFailed, cancellationToken),
            await durations.Select(x => (double?)x.DurationMs).AverageAsync(cancellationToken),
            reviewMetrics);
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
        .Include(x => x.ExtractionRuns).ThenInclude(x => x.Warnings)
        .Include(x => x.ReviewSessions).ThenInclude(x => x.Actions);
}

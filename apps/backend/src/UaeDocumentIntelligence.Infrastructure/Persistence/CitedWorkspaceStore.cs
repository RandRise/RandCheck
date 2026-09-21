using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedWorkspaceStore(AppDbContext db) : ICitedWorkspaceStore
{
    public async Task<ReviewedSourcesView> ListSourcesAsync(Guid organizationId, int offset, CancellationToken ct)
    {
        var sources = await (from review in db.ReviewSessions.AsNoTracking()
            join document in db.Documents.AsNoTracking() on review.DocumentId equals document.Id
            where review.OrganizationId == organizationId && document.OrganizationId == organizationId && review.Status == ReviewStatus.Completed
            orderby review.CompletedAtUtc descending, review.Id
            select new ReviewedSourceView(document.Id, document.OriginalFilename, review.Id,
                review.ExtractionRunId, review.RevisionNumber, review.CompletedAtUtc))
            .Skip(offset).Take(51).ToListAsync(ct);
        return new(sources.Take(50).ToArray(), sources.Count > 50 && offset + 50 <= 10000 ? offset + 50 : null);
    }

    public Task<ExtractionPage?> GetPageAsync(Guid organizationId, Guid extractionRunId, int pageNumber, CancellationToken ct) =>
        db.ExtractionPages.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
            x.ExtractionRunId == extractionRunId && x.PageNumber == pageNumber, ct);

    public Task<ReviewAction?> GetActionAsync(Guid organizationId, Guid reviewSessionId, Guid actionId, CancellationToken ct) =>
        db.ReviewActions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
            x.ReviewSessionId == reviewSessionId && x.Id == actionId, ct);
}

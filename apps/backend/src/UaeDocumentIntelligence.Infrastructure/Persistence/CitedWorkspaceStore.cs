using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedWorkspaceStore(AppDbContext db) : ICitedWorkspaceStore
{
    public Task<ReviewedSourceView?> GetReviewedSourceAsync(Guid organizationId, Guid documentId, Guid reviewId, CancellationToken ct) =>
        (from review in db.ReviewSessions.AsNoTracking()
         join document in db.Documents.AsNoTracking() on review.DocumentId equals document.Id
         join extraction in db.ExtractionRuns.AsNoTracking() on review.ExtractionRunId equals extraction.Id
         where review.OrganizationId == organizationId && document.OrganizationId == organizationId &&
             extraction.OrganizationId == organizationId && review.Id == reviewId && document.Id == documentId &&
             extraction.DocumentId == documentId && review.Status == ReviewStatus.Completed &&
             (extraction.Status == ExtractionRunStatus.Succeeded || extraction.Status == ExtractionRunStatus.Partial)
         select new ReviewedSourceView(document.Id, document.OriginalFilename, review.Id,
             review.ExtractionRunId, review.RevisionNumber, review.CompletedAtUtc)).SingleOrDefaultAsync(ct);

    public async Task<RecentQuestionsView> ListRecentQuestionsAsync(Guid organizationId, Guid actorId, CancellationToken ct)
    {
        // Filter before taking the bounded page: an ineligible saved corpus must not
        // disclose its question text, even when the run had no evidence hits.
        // Completed answers must match the public saved-question reader's supported
        // reviewed or deterministic no-evidence shapes; optional pilot answers do not.
        var questions = await db.CitedRetrievalRuns.AsNoTracking()
            .Where(run => run.OrganizationId == organizationId && run.ActorId == actorId &&
                db.Actors.Any(actor => actor.OrganizationId == organizationId && actor.Id == actorId && actor.IsActive) &&
                (run.Status == CitedRetrievalStatus.Retrieved || run.Status == CitedRetrievalStatus.Completed &&
                    run.Answer != null && (run.PolicyVersion == CitedReviewedQuestionHandler.Policy &&
                        run.Answer.ProviderAlias == CitedReviewedQuestionHandler.Provider && run.Answer.ModelVersion == "none" ||
                        !run.Hits.Any() && run.Answer.Status == CitedAnswerStatus.InsufficientEvidence &&
                        run.Answer.ReasonCode == "no_relevant_evidence" && run.Answer.ProviderAlias == "evidence-only" &&
                        run.Answer.ModelVersion == "none" && run.Answer.PromptVersion == CitedQuestionHandler.NoEvidencePolicy &&
                        !run.Answer.Claims.Any())) &&
                run.Sources.Any() && !run.Sources.Any(source => !db.CitedDocumentIndexes.Any(index =>
                    index.OrganizationId == organizationId && index.Id == source.IndexId && index.Status == DocumentIndexStatus.Ready &&
                    db.Documents.Any(document => document.OrganizationId == organizationId && document.Id == index.DocumentId) &&
                    db.ReviewSessions.Any(review => review.OrganizationId == organizationId && review.Id == index.ReviewSessionId &&
                        review.DocumentId == index.DocumentId && review.ExtractionRunId == index.ExtractionRunId && review.Status == ReviewStatus.Completed) &&
                    db.ExtractionRuns.Any(extraction => extraction.OrganizationId == organizationId && extraction.Id == index.ExtractionRunId &&
                        extraction.DocumentId == index.DocumentId && (extraction.Status == ExtractionRunStatus.Succeeded || extraction.Status == ExtractionRunStatus.Partial)))))
            .OrderByDescending(run => run.CreatedAtUtc).ThenBy(run => run.Id).Take(20)
            .Select(run => new RecentQuestionView(run.Id, run.Question, run.CreatedAtUtc, run.Sources.Count,
                run.PolicyVersion == CitedReviewedQuestionHandler.Policy ? "reviewed" : "evidence"))
            .ToArrayAsync(ct);
        return new(questions);
    }

    public async Task<ReviewedSourcesView> ListSourcesAsync(Guid organizationId, int offset, CancellationToken ct, string? search = null)
    {
        // Treat %, _ and the escape character as literal filename text, not SQL wildcards.
        var pattern = string.IsNullOrEmpty(search) ? null : $"%{search.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_")}%";
        var sources = await (from review in db.ReviewSessions.AsNoTracking()
            join document in db.Documents.AsNoTracking() on review.DocumentId equals document.Id
            where review.OrganizationId == organizationId && document.OrganizationId == organizationId && review.Status == ReviewStatus.Completed
                && (pattern == null || EF.Functions.ILike(document.OriginalFilename, pattern, "~"))
            orderby review.CompletedAtUtc descending, review.Id
            select new ReviewedSourceView(document.Id, document.OriginalFilename, review.Id,
                review.ExtractionRunId, review.RevisionNumber, review.CompletedAtUtc))
            .Skip(offset).Take(CitedSourceLimits.PageSize + 1).ToListAsync(ct);
        return new(sources.Take(CitedSourceLimits.PageSize).ToArray(),
            sources.Count > CitedSourceLimits.PageSize && offset + CitedSourceLimits.PageSize <= CitedSourceLimits.MaximumOffset
                ? offset + CitedSourceLimits.PageSize : null);
    }

    public Task<ExtractionPage?> GetPageAsync(Guid organizationId, Guid extractionRunId, int pageNumber, CancellationToken ct) =>
        db.ExtractionPages.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
            x.ExtractionRunId == extractionRunId && x.PageNumber == pageNumber, ct);

    public Task<ReviewAction?> GetActionAsync(Guid organizationId, Guid reviewSessionId, Guid actionId, CancellationToken ct) =>
        db.ReviewActions.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
            x.ReviewSessionId == reviewSessionId && x.Id == actionId, ct);
}

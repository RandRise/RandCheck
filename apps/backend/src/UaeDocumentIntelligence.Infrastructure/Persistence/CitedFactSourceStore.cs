using System.Data;
using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedFactSourceStore(AppDbContext db) : ICitedFactSourceStore
{
    public async Task<CitedFactCorpus> LoadAsync(Guid organizationId, Guid actorId, Guid runId, CancellationToken ct)
    {
        // A consistent read of current authorization, the frozen selected corpus, and completed decisions.
        await using var transaction = await db.Database.BeginTransactionAsync(
            db.Database.IsNpgsql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, ct);
        if (!await db.Actors.AsNoTracking().AnyAsync(a => a.Id == actorId && a.OrganizationId == organizationId && a.IsActive, ct))
            throw Missing();
        var run = await db.CitedRetrievalRuns.AsNoTracking().Include(r => r.Sources)
            .SingleOrDefaultAsync(r => r.Id == runId && r.OrganizationId == organizationId && r.ActorId == actorId, ct);
        if (run is null) throw Missing();
        if (run.Status is not (CitedRetrievalStatus.Retrieved or CitedRetrievalStatus.Completed) || run.Sources.Count is < 1 or > 10)
            throw new CitedWorkflowException("typed_corpus_not_ready", "The reviewed corpus is unavailable.");
        var documents = new List<CitedTypedDocument>();
        foreach (var source in run.Sources.OrderBy(s => s.IndexId))
        {
            var index = await db.CitedDocumentIndexes.AsNoTracking().Include(i => i.Chunks).ThenInclude(c => c.Spans)
                .SingleOrDefaultAsync(i => i.Id == source.IndexId && i.OrganizationId == organizationId, ct);
            if (index is null || index.Status != DocumentIndexStatus.Ready) throw Missing();
            documents.Add(await ProjectAsync(db, organizationId, index, ct));
        }
        await transaction.CommitAsync(ct);
        return new(run.Id, run.Question, documents.AsReadOnly());
    }

    internal static async Task<CitedTypedDocument> ProjectAsync(AppDbContext db, Guid organizationId,
        CitedDocumentIndex index, CancellationToken ct)
    {
            var document = await db.Documents.AsNoTracking().AsSplitQuery()
                .Include(d => d.ReviewSessions.Where(r => r.Id == index.ReviewSessionId)).ThenInclude(r => r.Actions)
                .Include(d => d.ExtractionRuns.Where(r => r.Id == index.ExtractionRunId)).ThenInclude(r => r.Fields).ThenInclude(f => f.Evidence)
                .Include(d => d.ExtractionRuns.Where(r => r.Id == index.ExtractionRunId)).ThenInclude(r => r.Pages)
                .SingleOrDefaultAsync(d => d.Id == index.DocumentId && d.OrganizationId == organizationId, ct) ?? throw Missing();
            var review = document.ReviewSessions.SingleOrDefault(r => r.Id == index.ReviewSessionId) ?? throw Missing();
            var extraction = document.ExtractionRuns.SingleOrDefault(r => r.Id == index.ExtractionRunId) ?? throw Missing();
            return CitedTypedFactProjection.Project(index, document, review, extraction);
    }

    private static CitedWorkflowException Missing() => new("typed_corpus_not_found", "The reviewed corpus was not found.", 404);
}

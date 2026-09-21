using Microsoft.EntityFrameworkCore;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed class CitedCaseSourceStore(AppDbContext db, IExecutionContext execution) : ICitedCaseSourceStore
{
    public async Task<CitedCaseSnapshot> ReadAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Actors.AsNoTracking().AnyAsync(a => a.OrganizationId == execution.OrganizationId &&
                a.Id == execution.ActorId && a.IsActive, ct)) throw Missing();
        var item = await db.ReconciliationCases.AsNoTracking().Include(c => c.DocumentLinks)
            .SingleOrDefaultAsync(c => c.OrganizationId == execution.OrganizationId && c.Id == id, ct) ?? throw Missing();
        var sources = new List<CitedCaseSource>();
        foreach (var link in item.DocumentLinks.OrderBy(l => l.Role))
        {
            var document = await db.Documents.AsNoTracking().Include(d => d.ReviewSessions)
                .SingleOrDefaultAsync(d => d.OrganizationId == execution.OrganizationId && d.Id == link.DocumentId, ct) ?? throw Missing();
            var review = document.ReviewSessions.SingleOrDefault(r => r.Id == link.ReviewSessionId &&
                r.ExtractionRunId == link.ExtractionRunId && r.Status == ReviewStatus.Completed);
            if (review is null || document.Sha256 != link.Sha256) throw Missing();
            sources.Add(new(document.Id, document.OriginalFilename, review.Id, review.ExtractionRunId, review.RevisionNumber));
        }
        return new(item.Id, item.Name, item.ConcurrencyToken, sources.AsReadOnly());
    }

    private static CitedWorkflowException Missing() => new("case_sources_unavailable", "The case sources are unavailable to this user.", 404);
}

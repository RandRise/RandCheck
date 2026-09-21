using System.Text;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record ReviewedSourceView(Guid DocumentId, string Filename, Guid ReviewSessionId,
    Guid ExtractionRunId, int RevisionNumber, DateTimeOffset? CompletedAtUtc);
public sealed record ReviewedSourcesView(IReadOnlyList<ReviewedSourceView> Sources, int? NextOffset);
public sealed record EvidencePageView(int PageNumber, int ByteStart, int ByteEnd,
    string Before, string Quote, string After);
public sealed record EvidenceReviewView(Guid ActionId, string Action, string FieldKey, int Occurrence,
    string? Reason, DateTimeOffset CreatedAtUtc);
public sealed record EvidenceLocationView(Guid RetrievalRunId, Guid HitId, Guid IndexId, Guid DocumentId,
    string Filename, Guid ExtractionRunId, Guid ReviewSessionId, string Kind, string? ReviewedFact,
    EvidenceReviewView? Review, EvidencePageView? Page);

public interface ICitedWorkspaceStore
{
    Task<ReviewedSourcesView> ListSourcesAsync(Guid organizationId, int offset, CancellationToken ct);
    Task<ExtractionPage?> GetPageAsync(Guid organizationId, Guid extractionRunId, int pageNumber, CancellationToken ct);
    Task<ReviewAction?> GetActionAsync(Guid organizationId, Guid reviewSessionId, Guid actionId, CancellationToken ct);
}

public sealed class CitedWorkspaceHandler(ICitedWorkspaceStore workspace, ICitedRetrievalStore retrieval,
    ICitedQuestionRepository repository, IExecutionContext execution, ICitedReviewedQuestionStore? reviewed = null)
{
    public async Task<ReviewedSourcesView> SourcesAsync(int offset, CancellationToken ct)
    {
        if (offset is < 0 or > 10000) throw new ArgumentException("Invalid source offset.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        return await workspace.ListSourcesAsync(execution.OrganizationId, offset, deadline.Token);
    }

    public async Task<EvidenceLocationView> EvidenceAsync(Guid runId, Guid hitId, Guid? spanId, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        ct = deadline.Token;
        var run = await repository.GetRunAsync(execution.OrganizationId, runId, ct);
        if (run is null || run.ActorId != execution.ActorId) throw Missing();
        if (run.PolicyVersion == CitedReviewedQuestionHandler.Policy)
        {
            if (reviewed is null) throw Missing();
            await reviewed.EnsureReadAccessAsync(execution.OrganizationId, execution.ActorId, runId, ct);
        }
        var hit = run.Hits.SingleOrDefault(x => x.Id == hitId) ?? throw Missing();
        if (!run.Sources.Any(x => x.IndexId == hit.IndexId)) throw Missing();
        var index = await retrieval.GetIndexHeaderAsync(execution.OrganizationId, hit.IndexId, ct) ?? throw Missing();
        if (index.Status != DocumentIndexStatus.Ready)
            throw new CitedWorkflowException("evidence_unavailable", "This source is no longer eligible for evidence navigation.");
        var chunk = (await retrieval.GetHitChunksAsync(run, ct)).SingleOrDefault(x => x.Id == hit.ChunkId &&
            x.IndexId == index.Id && x.ExtractionRunId == index.ExtractionRunId && x.ReviewSessionId == index.ReviewSessionId)
            ?? throw Missing();
        EvidenceReviewView? review = null;
        if (chunk.Kind == DocumentChunkKind.ReviewedFact)
        {
            if (!chunk.ReviewActionId.HasValue) throw Missing();
            var action = await workspace.GetActionAsync(execution.OrganizationId, index.ReviewSessionId, chunk.ReviewActionId.Value, ct)
                ?? throw Missing();
            review = new(action.Id, action.Action.ToString(), action.FieldKey, action.OccurrenceIndex, action.Reason, action.CreatedAtUtc);
        }
        EvidencePageView? page = null;
        if (spanId.HasValue)
        {
            var span = chunk.Spans.SingleOrDefault(x => x.Id == spanId) ?? throw Missing();
            var source = await workspace.GetPageAsync(execution.OrganizationId, index.ExtractionRunId, span.PageNumber, ct)
                ?? throw Missing();
            var bytes = Encoding.UTF8.GetBytes(source.CanonicalText);
            if (bytes.Length > 1048576)
                throw new CitedWorkflowException("evidence_page_too_large", "This page exceeds the evidence viewer's size limit.", 413);
            try
            {
                var encoding = new UTF8Encoding(false, true);
                if (span.ByteStart < 0 || span.ByteEnd <= span.ByteStart || span.ByteEnd > bytes.Length) throw new ArgumentException();
                var quote = encoding.GetString(bytes.AsSpan(span.ByteStart, span.ByteEnd - span.ByteStart));
                if (quote != span.Quote) throw new ArgumentException();
                page = new(source.PageNumber, span.ByteStart, span.ByteEnd,
                    encoding.GetString(bytes.AsSpan(0, span.ByteStart)), quote, encoding.GetString(bytes.AsSpan(span.ByteEnd)));
            }
            catch (ArgumentException)
            { throw new CitedWorkflowException("invalid_evidence_span", "The saved evidence could not be verified.", 409); }
        }
        else if (review is null) throw Missing();
        return new(run.Id, hit.Id, index.Id, index.DocumentId, index.Filename, index.ExtractionRunId,
            index.ReviewSessionId, chunk.Kind.ToString(), review is null ? null : chunk.Content, review, page);
    }

    private static CitedWorkflowException Missing() => new("evidence_not_found", "Evidence was not found.", 404);
}

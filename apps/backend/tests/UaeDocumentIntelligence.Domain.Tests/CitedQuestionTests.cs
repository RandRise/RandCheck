using System.Text;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class CitedQuestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Retrieved_is_frozen_evidence_not_a_completed_answer()
    {
        var (doc, index, chunk) = Indexed();
        var request = Request(doc, index);
        var hit = request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        request.MarkRetrieved(10);
        Assert.Equal(CitedRetrievalStatus.Retrieved, request.Status);
        Assert.Null(request.Answer);
        Assert.Null(request.CompletedAtUtc);
        Assert.Throws<InvalidOperationException>(() => request.AddHit(Guid.NewGuid(), chunk, 2, 1, null, null));
        Assert.Throws<InvalidOperationException>(() => request.MarkRetrieved(11));
        Complete(request, [new("Fact", [new(hit.Id, chunk.Spans.Single().Id)])], [chunk]);
        Assert.Equal(CitedRetrievalStatus.Completed, request.Status);
    }

    [Fact]
    public void Retrieved_duration_cannot_be_rewritten_by_answer_generation()
    {
        var (doc, index, chunk) = Indexed();
        var request = Request(doc, index);
        var hit = request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        request.MarkRetrieved(12);
        Assert.Throws<ArgumentException>(() => Complete(request,
            [new("Fact", [new(hit.Id, chunk.Spans.Single().Id)])], [chunk]));
        Assert.Null(request.Answer);
        Assert.Equal(12, request.RetrievalDurationMs);
    }

    [Fact]
    public void Index_requires_completed_review_of_same_document()
    {
        var (doc, run, review) = Fixture();
        var pending = new ReviewSession(Guid.NewGuid(), doc.OrganizationId, doc.Id, run.Id, 2, Now);
        Assert.Throws<ArgumentException>(() => new CitedDocumentIndex(Guid.NewGuid(), doc, pending, "page", "1", Now));
        var other = Fixture();
        Assert.Throws<ArgumentException>(() => new CitedDocumentIndex(Guid.NewGuid(), other.Doc, review, "page", "1", Now));
    }

    [Fact]
    public void Page_chunks_preserve_utf8_text_and_hash_and_reject_split_characters()
    {
        var (doc, run, review) = Fixture();
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page", "1", Now);
        var page = run.Pages.Single();
        Assert.Throws<ArgumentException>(() => index.AddPageChunk(Guid.NewGuid(), 0, page, 1, 2));
        Assert.Throws<ArgumentException>(() => index.AddPageChunk(Guid.NewGuid(), 0, page, 0, 999));
        var chunk = index.AddPageChunk(Guid.NewGuid(), 0, page, 0, Encoding.UTF8.GetByteCount(page.CanonicalText));
        Assert.Equal(page.CanonicalText, chunk.Content);
        Assert.Equal(64, chunk.ContentSha256.Length);
        Assert.Equal(page.CanonicalText, chunk.Spans.Single().Quote);
        Assert.Throws<ArgumentException>(() => index.AddPageChunk(Guid.NewGuid(), 1, Fixture().Run.Pages.Single(), 0, 2));
        index.Publish();
        Assert.Throws<InvalidOperationException>(() => index.AddPageChunk(Guid.NewGuid(), 1, page, 0, 2));
        Assert.Throws<InvalidOperationException>(() => index.Publish());
    }

    [Fact]
    public void Corrected_fact_preserves_review_decision_and_original_machine_quote()
    {
        var (doc, run, review) = Fixture(ReviewActionType.Corrected);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "facts", "1", Now);
        var fact = index.AddReviewedFact(Guid.NewGuid(), 0, review, "total", 0, run);
        Assert.Equal("total[0]: 110.00", fact.Content);
        Assert.Equal(review.Actions.Single().Id, fact.ReviewActionId);
        Assert.Equal("100.00", fact.Spans.Single().Quote);
        Assert.Equal(run.Fields.Single().Evidence.Single().Id, fact.Spans.Single().FieldEvidenceId);
    }

    [Fact]
    public void Reviewer_added_fact_has_review_provenance_without_fabricated_page()
    {
        var (doc, run, review) = Fixture(ReviewActionType.Added);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "facts", "1", Now);
        var fact = index.AddReviewedFact(Guid.NewGuid(), 0, review, "terms", 0, run);
        Assert.Equal("terms[0]: Net 30", fact.Content);
        Assert.NotNull(fact.ReviewActionId);
        Assert.Empty(fact.Spans);
    }

    [Fact]
    public void Cleared_values_cannot_be_indexed_as_facts()
    {
        var (doc, run, review) = Fixture(ReviewActionType.Cleared);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "facts", "1", Now);
        Assert.Throws<ArgumentException>(() => index.AddReviewedFact(Guid.NewGuid(), 0, review, "total", 0, run));
    }

    [Fact]
    public void Retrieval_scope_rejects_foreign_building_retired_and_duplicate_documents()
    {
        var (doc, run, review) = Fixture();
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page", "1", Now);
        Assert.Throws<ArgumentException>(() => Request(doc, index));
        index.AddPageChunk(Guid.NewGuid(), 0, run.Pages.Single(), 0, 2);
        index.Publish();
        Assert.Throws<ArgumentException>(() => Request(Fixture().Doc, index));
        Assert.Throws<ArgumentException>(() => Request(doc, index, index));
        index.Retire();
        Assert.Throws<ArgumentException>(() => Request(doc, index));
    }

    [Fact]
    public void Hits_cannot_escape_corpus_or_duplicate_rank_or_chunk()
    {
        var (doc, index, chunk) = Indexed();
        var request = Request(doc, index);
        Assert.Throws<ArgumentException>(() => request.AddHit(Guid.NewGuid(), Indexed().Chunk, 1, 1, null, null));
        Assert.Throws<ArgumentException>(() => request.AddHit(Guid.NewGuid(), chunk, 1, double.NaN, null, null));
        request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        Assert.Throws<ArgumentException>(() => request.AddHit(Guid.NewGuid(), chunk, 2, 1, null, null));
    }

    [Theory]
    [InlineData("unknown_hit")]
    [InlineData("unknown_span")]
    [InlineData("uncited_claim")]
    [InlineData("missing_page_span")]
    public void Invalid_citations_leave_run_unmodified(string failure)
    {
        var (doc, index, chunk) = Indexed();
        var request = Request(doc, index);
        var hit = request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        var citation = new CitationInput(failure == "unknown_hit" ? Guid.NewGuid() : hit.Id,
            failure == "unknown_span" ? Guid.NewGuid() :
            failure == "missing_page_span" ? null : chunk.Spans.Single().Id);
        CitedClaimInput[] claims = [new("A fact", failure == "uncited_claim" ? [] : [citation])];
        Assert.Throws<ArgumentException>(() => Complete(request, claims, [chunk]));
        Assert.Equal(CitedRetrievalStatus.Running, request.Status);
        Assert.Null(request.Answer);
        Assert.Null(request.CompletedAtUtc);
    }

    [Fact]
    public void Answer_claim_and_citation_collections_are_readonly_and_completion_is_terminal()
    {
        var (doc, index, chunk) = Indexed();
        var request = Request(doc, index);
        var hit = request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        Complete(request, [new("The total is 100.00.", [new(hit.Id, chunk.Spans.Single().Id)])], [chunk]);
        Assert.Throws<NotSupportedException>(() => ((ICollection<AnswerClaim>)request.Answer!.Claims).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<AnswerCitation>)request.Answer!.Claims.Single().Citations).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<RetrievalSource>)request.Sources).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<RetrievalHit>)request.Hits).Clear());
        Assert.Throws<InvalidOperationException>(() => request.Fail("late", Now));
        Assert.Throws<InvalidOperationException>(() => Complete(request, [], []));
    }

    [Fact]
    public void Refusals_are_successful_results_with_no_claims_and_required_reason()
    {
        var (doc, index, _) = Indexed();
        var request = Request(doc, index);
        request.Complete(Guid.NewGuid(), CitedAnswerStatus.InsufficientEvidence, "no_relevant_evidence",
            [], [], "evidence-only", "none", "1", 10, 0, null, null, Now);
        Assert.Equal(CitedRetrievalStatus.Completed, request.Status);
        Assert.Empty(request.Answer!.Claims);
        Assert.Null(request.Answer.EstimatedCost);
    }

    [Fact]
    public void Retiring_index_does_not_change_existing_answer_provenance()
    {
        var (doc, index, chunk) = Indexed();
        var request = Request(doc, index);
        var hit = request.AddHit(Guid.NewGuid(), chunk, 1, 1, null, null);
        Complete(request, [new("Fact", [new(hit.Id, chunk.Spans.Single().Id)])], [chunk]);
        index.Retire(true);
        Assert.Equal(index.Id, request.Sources.Single().IndexId);
        Assert.Equal(chunk.Id, request.Answer!.Claims.Single().Citations.Single().ChunkId);
    }

    private static void Complete(CitedRetrievalRun run, CitedClaimInput[] claims, DocumentChunk[] chunks) =>
        run.Complete(Guid.NewGuid(), CitedAnswerStatus.Answered, null, claims, chunks,
            "evidence-only", "none", "1", 10, 0, null, null, Now);

    private static CitedRetrievalRun Request(Document doc, params CitedDocumentIndex[] indexes) =>
        new(Guid.NewGuid(), doc.OrganizationId, doc.CreatedByActorId, "What is the total?",
            Guid.NewGuid().ToString(), "lexical-1", "{\"mode\":\"lexical\"}", indexes, Now);

    private static (Document Doc, CitedDocumentIndex Index, DocumentChunk Chunk) Indexed()
    {
        var (doc, extraction, review) = Fixture();
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "page", "1", Now);
        var page = extraction.Pages.Single();
        var chunk = index.AddPageChunk(Guid.NewGuid(), 0, page, 0, Encoding.UTF8.GetByteCount(page.CanonicalText));
        index.Publish();
        return (doc, index, chunk);
    }

    private static (Document Doc, ExtractionRun Run, ReviewSession Review) Fixture(
        ReviewActionType actionType = ReviewActionType.Confirmed)
    {
        var doc = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            Guid.NewGuid().ToString(), "application/pdf", 100, new string('a', 64), Now);
        var run = doc.QueueExtraction(Guid.NewGuid(), 1, "local", "1", "2.0", "test", Now);
        run.Start(Now);
        run.AddPage(Guid.NewGuid(), 1, "é Total: 100.00", 0);
        var field = run.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, "100.00", 1, Now);
        run.AddEvidence(field, Guid.NewGuid(), 1, "100.00", 10, 16);
        run.RecordProviderMetrics(1, 1, null, null, null);
        run.Succeed("invoice", "en", Now, "test", "1");
        doc.RecordExtractionSuccess(run, Now);
        var review = doc.OpenReview(Guid.NewGuid(), doc.CreatedByActorId, Now);
        var machineAction = actionType == ReviewActionType.Added ? ReviewActionType.Confirmed : actionType;
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 1, machineAction, "total", 0, field.Id,
            machineAction == ReviewActionType.Corrected ? FieldValueType.Decimal : null,
            machineAction == ReviewActionType.Corrected ? "110.00" : null,
            machineAction is ReviewActionType.Corrected or ReviewActionType.Cleared ? "Checked source" : null, Now);
        if (actionType == ReviewActionType.Added)
            review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 2, ReviewActionType.Added, "terms", 0,
                null, FieldValueType.String, "Net 30", "Added from review", Now);
        review.Complete(run.Fields, Now);
        return (doc, run, review);
    }
}

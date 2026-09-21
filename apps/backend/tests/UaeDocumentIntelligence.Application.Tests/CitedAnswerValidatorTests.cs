using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class CitedAnswerValidatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-07T00:00:00Z");

    [Fact]
    public void Valid_citation_resolves_canonical_unicode_evidence_but_does_not_prove_claim_support()
    {
        var (run, page, _, _) = Fixture();
        var payload = Proposal(run, page);
        payload["claims"]![0]!["text"] = "The total is 999.00 and payment is approved.";
        var result = Validate(payload, run, page);
        Assert.False(result.SemanticSupportEstablished);
        Assert.Equal(CitedAnswerValidator.Version, result.ValidatorVersion);
        var citation = Assert.Single(Assert.Single(result.Claims).Citations);
        Assert.Equal("é Total: 100.00", citation.OriginalPageEvidence!.Quote);
        Assert.Equal(16, citation.OriginalPageEvidence.ByteEnd);
        Assert.Equal(page.IndexId, citation.IndexId);
        Assert.Equal(CitedRetrievalStatus.Retrieved, run.Status);
        Assert.Null(run.Answer); // Structural success has no publication/persistence side effect.
        Assert.Throws<NotSupportedException>(() => ((IList<StructurallyValidatedClaim>)result.Claims).Clear());
    }

    [Theory]
    [InlineData("unknown_field")]
    [InlineData("wrong_case")]
    [InlineData("missing_schema")]
    [InlineData("wrong_schema")]
    [InlineData("numeric_status")]
    [InlineData("null_claims")]
    [InlineData("null_claim")]
    [InlineData("blank_claim")]
    [InlineData("long_claim")]
    [InlineData("uncited_claim")]
    [InlineData("duplicate_citation")]
    [InlineData("duplicate_claim")]
    [InlineData("unknown_hit")]
    [InlineData("unknown_span")]
    [InlineData("missing_page_span")]
    [InlineData("fake_quote")]
    [InlineData("fake_document")]
    [InlineData("external_url")]
    [InlineData("answered_reason")]
    [InlineData("partial_without_missing_parts")]
    [InlineData("refusal_with_claim")]
    [InlineData("null_citation")]
    [InlineData("too_many_citations")]
    [InlineData("too_many_claims")]
    public void Malformed_fabricated_or_uncited_proposals_fail_closed_without_echoing_text(string mutation)
    {
        var (run, page, _, _) = Fixture();
        var payload = Proposal(run, page);
        var claim = payload["claims"]![0]!;
        var citation = claim["citations"]![0]!;
        switch (mutation)
        {
            case "unknown_field": payload["answer"] = "SECRET_SOURCE_TEXT"; break;
            case "wrong_case": payload["SchemaVersion"] = "1.0"; break;
            case "missing_schema": payload.Remove("schemaVersion"); break;
            case "wrong_schema": payload["schemaVersion"] = "2"; break;
            case "numeric_status": payload["status"] = 0; break;
            case "null_claims": payload["claims"] = null; break;
            case "null_claim": payload["claims"]![0] = null; break;
            case "blank_claim": claim["text"] = " "; break;
            case "long_claim": claim["text"] = new string('x', 4001); break;
            case "uncited_claim": claim["citations"] = new JsonArray(); break;
            case "duplicate_citation": claim["citations"]!.AsArray().Add(citation.DeepClone()); break;
            case "duplicate_claim": payload["claims"]!.AsArray().Add(claim.DeepClone()); break;
            case "unknown_hit": citation["hitId"] = Guid.NewGuid(); break;
            case "unknown_span": citation["spanId"] = Guid.NewGuid(); break;
            case "missing_page_span": citation["spanId"] = null; break;
            case "fake_quote": citation["quote"] = "SECRET_SOURCE_TEXT"; break;
            case "fake_document": citation["documentId"] = Guid.NewGuid(); break;
            case "external_url": citation["url"] = "https://example.invalid"; break;
            case "answered_reason": payload["reasonCode"] = "incomplete_evidence"; break;
            case "partial_without_missing_parts": payload["status"] = "PartiallyAnswered"; payload["reasonCode"] = "incomplete_evidence"; break;
            case "refusal_with_claim": payload["status"] = "InsufficientEvidence"; payload["reasonCode"] = "incomplete_evidence"; break;
            case "null_citation": claim["citations"]![0] = null; break;
            case "too_many_citations": claim["citations"] = new JsonArray(Enumerable.Range(0, 9).Select(_ => citation.DeepClone()).ToArray()); break;
            case "too_many_claims": payload["claims"] = new JsonArray(Enumerable.Range(0, 21).Select(_ => claim.DeepClone()).ToArray()); break;
        }
        var error = Assert.Throws<CitedWorkflowException>(() => Validate(payload, run, page));
        Assert.Equal("invalid_answer_proposal", error.Code);
        Assert.DoesNotContain("SECRET_SOURCE_TEXT", error.Message);
        Assert.Null(error.InnerException);
        Assert.Null(run.Answer);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"1.0\",\"schemaVersion\":\"1.0\"}")]
    [InlineData("{\"claims\":[{\"text\":\"a\",\"text\":\"b\"}]}")]
    [InlineData("{\"claims\":[{\"citations\":[{\"hitId\":null,\"hitId\":null}]}]}")]
    [InlineData("{\"schemaVersion\":\"1.0\",}")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Ambiguous_or_nonobject_json_is_rejected(string json)
    {
        var (run, page, _, _) = Fixture();
        Assert.Throws<CitedWorkflowException>(() => CitedAnswerValidator.Validate(Encoding.UTF8.GetBytes(json), run, [page]));
    }

    [Fact]
    public void Invalid_utf8_depth_and_actual_payload_bytes_are_bounded()
    {
        var (run, page, _, _) = Fixture();
        foreach (var bytes in new[] { new byte[] { 0xff }, new byte[65537],
                     Encoding.UTF8.GetBytes(new string('[', 15) + new string(']', 15)) })
            Assert.Equal("invalid_answer_proposal", Assert.Throws<CitedWorkflowException>(() =>
                CitedAnswerValidator.Validate(bytes, run, [page])).Code);
    }

    [Fact]
    public void Other_runs_spans_nonretrieved_and_foreign_chunks_cannot_be_substituted()
    {
        var (run, page, _, index) = Fixture();
        var (foreign, foreignPage, _, _) = Fixture();
        var payload = Proposal(run, page);
        Assert.Throws<CitedWorkflowException>(() => Validate(payload, run, foreignPage));
        Assert.Throws<CitedWorkflowException>(() => Validate(payload, run, page, foreignPage));
        Assert.Throws<CitedWorkflowException>(() => Validate(payload, run));
        payload["claims"]![0]!["citations"]![0]!["hitId"] = foreign.Hits.Single().Id;
        Assert.Throws<CitedWorkflowException>(() => Validate(payload, run, page));
        var other = new CitedRetrievalRun(Guid.NewGuid(), run.OrganizationId, run.ActorId, "other", "other", "1", "{}", [index], Now);
        other.AddHit(Guid.NewGuid(), page, 1, 1, null, null);
        other.MarkRetrieved(1);
        Assert.Throws<CitedWorkflowException>(() => Validate(Proposal(run, page), other, page));
    }

    [Fact]
    public void Corrected_fact_retains_review_value_separately_from_original_page_quote()
    {
        var (run, _, fact, _) = Fixture(factHit: true);
        var proposal = Proposal(run, fact);
        var citation = Assert.Single(Assert.Single(Validate(proposal, run, fact).Claims).Citations);
        Assert.Equal("ReviewedFact", citation.ProvenanceType);
        Assert.Equal("100.00", citation.OriginalPageEvidence!.Quote);
        Assert.Equal("total[0]: 110.00", fact.Content);
        Assert.NotNull(citation.ReviewActionId);
        proposal["claims"]![0]!["citations"]![0]!["spanId"] = null;
        Assert.Null(Validate(proposal, run, fact).Claims.Single().Citations.Single().OriginalPageEvidence);
    }

    [Fact]
    public void Partial_proposal_must_preserve_explicit_unanswered_parts_without_implying_semantic_support()
    {
        var (run, page, _, _) = Fixture();
        var proposal = Proposal(run, page);
        proposal["status"] = "PartiallyAnswered";
        proposal["reasonCode"] = "incomplete_evidence";
        proposal["unansweredParts"] = new JsonArray("The payment date is not established.");
        var result = Validate(proposal, run, page);
        Assert.Single(result.UnansweredParts);
        Assert.False(result.SemanticSupportEstablished);
        Assert.Null(run.Answer);
    }

    [Fact]
    public void No_hits_refusal_is_structurally_valid_but_the_same_reason_with_hits_is_rejected()
    {
        var (run, page, _, index) = Fixture();
        var empty = new CitedRetrievalRun(Guid.NewGuid(), run.OrganizationId, run.ActorId, "unknown", "empty", "1", "{}", [index], Now);
        empty.MarkRetrieved(1);
        var payload = new JsonObject { ["schemaVersion"] = "1.0", ["status"] = "InsufficientEvidence",
            ["reasonCode"] = "no_relevant_evidence", ["claims"] = new JsonArray(), ["unansweredParts"] = new JsonArray() };
        Assert.Empty(Validate(payload, empty).Claims);
        Assert.Throws<CitedWorkflowException>(() => Validate(payload, run, page));
    }

    [Fact]
    public void Running_or_terminal_runs_cannot_validate_new_proposals()
    {
        var (run, page, _, _) = Fixture();
        run.Fail("test", Now);
        Assert.Equal("retrieval_not_ready", Assert.Throws<CitedWorkflowException>(() => Validate(Proposal(run, page), run, page)).Code);
    }

    private static CitedProposalValidation Validate(JsonObject value, CitedRetrievalRun run, params DocumentChunk[] chunks) =>
        CitedAnswerValidator.Validate(Encoding.UTF8.GetBytes(value.ToJsonString()), run, chunks);

    [Fact]
    public void Frozen_paid_date_claim_with_a_genuine_due_date_citation_is_blocked_after_structure_validation()
    {
        var (run, page, _, _) = Fixture(pageText: "Due date: 2026-08-31\nPayment terms: Net 30\nAmount due: AED 8820.00");
        var proposal = Proposal(run, page);
        proposal["claims"]![0]!["text"] = "The invoice was paid on 2026-08-31.";
        var structurallyValid = Validate(proposal, run, page);
        Assert.Contains("Due date: 2026-08-31", structurallyValid.Claims.Single().Citations.Single().OriginalPageEvidence!.Quote);
        Assert.False(structurallyValid.SemanticSupportEstablished);
        Assert.Equal("payment_event_not_verified", Assert.Throws<CitedWorkflowException>(() =>
            CitedPaymentClaimPolicy.Validate("On what date was the invoice paid?", structurallyValid)).Code);
        Assert.Null(run.Answer);
    }

    [Theory]
    [InlineData("100.00", true)]
    [InlineData("Total: 100.00", true)]
    [InlineData("8820.00", false)]
    [InlineData("total: 100.00", false)]
    [InlineData("", false)]
    [InlineData(" 100.00", false)]
    [InlineData(null, false)]
    public void Source_aware_excerpt_must_occur_exactly_in_the_referenced_page(string? excerpt, bool accepted)
    {
        var (run, page, _, _) = Fixture();
        var proposal = Proposal(run, page);
        proposal["schemaVersion"] = "1.1";
        proposal["claims"]![0]!["citations"]![0]!["supportText"] = excerpt;
        var bytes = Encoding.UTF8.GetBytes(proposal.ToJsonString());
        if (!accepted)
        {
            Assert.Throws<CitedWorkflowException>(() => CitedAnswerValidator.Validate(bytes, run, [page], true));
            return;
        }
        var result = CitedAnswerValidator.Validate(bytes, run, [page], true);
        Assert.Equal(CitedAnswerValidator.SourceVersion, result.ValidatorVersion);
        Assert.Equal(excerpt, result.Claims.Single().Citations.Single().SupportText);
        Assert.False(result.SemanticSupportEstablished);
        Assert.Null(run.Answer);
        // Membership is deliberately not a semantic proof, even when the claim lies.
        proposal["claims"]![0]!["text"] = "The total is 999.00.";
        Assert.False(CitedAnswerValidator.Validate(Encoding.UTF8.GetBytes(proposal.ToJsonString()), run, [page], true)
            .SemanticSupportEstablished);
    }

    [Fact]
    public void Source_aware_correction_cannot_bind_to_superseded_machine_value()
    {
        var (run, _, fact, _) = Fixture(factHit: true);
        var proposal = Proposal(run, fact);
        proposal["schemaVersion"] = "1.1";
        proposal["claims"]![0]!["text"] = "The reviewed total is 110.00.";
        var citation = proposal["claims"]![0]!["citations"]![0]!;
        citation["supportText"] = "100.00";
        Assert.Throws<CitedWorkflowException>(() => CitedAnswerValidator.Validate(
            Encoding.UTF8.GetBytes(proposal.ToJsonString()), run, [fact], true));
        citation["supportText"] = "total[0]: 110.00";
        var result = CitedAnswerValidator.Validate(Encoding.UTF8.GetBytes(proposal.ToJsonString()), run, [fact], true);
        Assert.Equal("100.00", result.Claims.Single().Citations.Single().OriginalPageEvidence!.Quote);
        Assert.Equal("total[0]: 110.00", result.Claims.Single().Citations.Single().SupportText);
    }

    [Fact]
    public void Source_aware_contract_cannot_be_downgraded_or_silently_applied_to_legacy_answers()
    {
        var (run, page, _, _) = Fixture();
        var proposal = Proposal(run, page);
        Assert.Throws<CitedWorkflowException>(() => CitedAnswerValidator.Validate(
            Encoding.UTF8.GetBytes(proposal.ToJsonString()), run, [page], true));
        proposal["claims"]![0]!["citations"]![0]!["supportText"] = null;
        Assert.Throws<CitedWorkflowException>(() => Validate(proposal, run, page));
        proposal["schemaVersion"] = "1.1";
        proposal["claims"]![0]!["citations"]![0]!["supportText"] = "100.00";
        Assert.Throws<CitedWorkflowException>(() => Validate(proposal, run, page));
    }

    private static JsonObject Proposal(CitedRetrievalRun run, DocumentChunk chunk) =>
        JsonSerializer.SerializeToNode(new { schemaVersion = "1.0", status = "Answered", reasonCode = (string?)null,
            claims = new[] { new { text = "The total is 100.00.", citations = new[] {
                new { hitId = run.Hits.Single().Id, spanId = (Guid?)chunk.Spans.Single().Id } } } },
            unansweredParts = Array.Empty<string>() })!.AsObject();

    private static (CitedRetrievalRun Run, DocumentChunk Page, DocumentChunk Fact, CitedDocumentIndex Index) Fixture(
        bool factHit = false, string pageText = "é Total: 100.00")
    {
        var doc = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf", Guid.NewGuid().ToString(),
            "application/pdf", 100, new string('a', 64), Now);
        var extraction = doc.QueueExtraction(Guid.NewGuid(), 1, "test", "1", "2.0", "test", Now);
        extraction.Start(Now);
        var page = extraction.AddPage(Guid.NewGuid(), 1, pageText, 0);
        var value = pageText == "é Total: 100.00" ? "100.00" : "8820.00";
        var field = extraction.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, value, 1, Now);
        var start = Encoding.UTF8.GetByteCount(pageText[..pageText.IndexOf(value, StringComparison.Ordinal)]);
        extraction.AddEvidence(field, Guid.NewGuid(), 1, value, start, start + Encoding.UTF8.GetByteCount(value));
        extraction.RecordProviderMetrics(1, 1, null, null, null);
        extraction.Succeed("invoice", "en", Now, "test", "1");
        doc.RecordExtractionSuccess(extraction, Now);
        var review = doc.OpenReview(Guid.NewGuid(), doc.CreatedByActorId, Now);
        review.AddAction(Guid.NewGuid(), doc.CreatedByActorId, 1, ReviewActionType.Corrected,
            "total", 0, field.Id, FieldValueType.Decimal, "110.00", "Corrected by reviewer", Now);
        review.Complete(extraction.Fields, Now);
        var index = new CitedDocumentIndex(Guid.NewGuid(), doc, review, "test", "1", Now);
        var chunk = index.AddPageChunk(Guid.NewGuid(), 0, page, 0, Encoding.UTF8.GetByteCount(page.CanonicalText));
        var fact = index.AddReviewedFact(Guid.NewGuid(), 1, review, "total", 0, extraction);
        index.Publish();
        var run = new CitedRetrievalRun(Guid.NewGuid(), doc.OrganizationId, doc.CreatedByActorId, "total?", "key", "1", "{}", [index], Now);
        run.AddHit(Guid.NewGuid(), factHit ? fact : chunk, 1, 1, null, null);
        run.MarkRetrieved(1);
        return (run, chunk, fact, index);
    }
}

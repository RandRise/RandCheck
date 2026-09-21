using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

// The provider may reference opaque evidence IDs, never supply authoritative source metadata.
public sealed record AnswerProposal
{
    public required string SchemaVersion { get; init; }
    public required string Status { get; init; }
    public required string? ReasonCode { get; init; }
    public required IReadOnlyList<ClaimProposal> Claims { get; init; }
    public required IReadOnlyList<string> UnansweredParts { get; init; }
}
public sealed record ClaimProposal
{
    public required string Text { get; init; }
    public required IReadOnlyList<CitationProposal> Citations { get; init; }
}
public sealed record CitationProposal
{
    public required Guid HitId { get; init; }
    public required Guid? SpanId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SupportText { get; init; }
}
public sealed record VerifiedCitation(Guid HitId, Guid? SpanId, Guid ChunkId, Guid IndexId,
    Guid ReviewSessionId, Guid ExtractionRunId, string ProvenanceType, Guid? ReviewActionId,
    SpanView? OriginalPageEvidence)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SupportText { get; init; }
}
public sealed record StructurallyValidatedClaim(string Text, IReadOnlyList<VerifiedCitation> Citations);
public sealed record CitedProposalValidation(string ValidatorVersion, CitedAnswerStatus Status,
    string? ReasonCode, IReadOnlyList<StructurallyValidatedClaim> Claims, IReadOnlyList<string> UnansweredParts)
{
    // Citation existence is not entailment, relevance, completeness, or injection resistance.
    public bool SemanticSupportEstablished => false;
}

public static class CitedAnswerValidator
{
    public const string Version = "citation-structure-v1";
    public const string SourceVersion = "citation-excerpt-binding-v2";
    public const int MaximumBytes = 65536;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> Reasons = ["no_relevant_evidence", "incomplete_evidence",
        "conflicting_evidence", "unsupported_scope", "unsafe_source"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static CitedProposalValidation Validate(ReadOnlyMemory<byte> utf8, CitedRetrievalRun run,
        IReadOnlyCollection<DocumentChunk> retrievedChunks, bool requireSupportText = false)
    {
        if (run.Status != CitedRetrievalStatus.Retrieved)
            throw new CitedWorkflowException("retrieval_not_ready", "Complete retrieval before validating an answer.");
        try
        {
            Require(utf8.Length is > 0 and <= MaximumBytes);
            _ = StrictUtf8.GetString(utf8.Span);
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 12 });
            RejectDuplicateProperties(document.RootElement);
            var proposal = document.RootElement.Deserialize<AnswerProposal>(Json);
            Require(proposal is not null && proposal.SchemaVersion == (requireSupportText ? "1.1" : "1.0"));
            if (!requireSupportText)
                foreach (var claim in document.RootElement.GetProperty("claims").EnumerateArray())
                    foreach (var citation in claim.GetProperty("citations").EnumerateArray())
                        Require(!citation.TryGetProperty("supportText", out _));
            var status = proposal!.Status switch
            {
                "Answered" => CitedAnswerStatus.Answered,
                "PartiallyAnswered" => CitedAnswerStatus.PartiallyAnswered,
                "InsufficientEvidence" => CitedAnswerStatus.InsufficientEvidence,
                _ => throw new ArgumentException()
            };
            Require(proposal.Claims is not null && proposal.UnansweredParts is not null);
            Require(proposal.Claims!.Count <= 20 && proposal.UnansweredParts!.Count <= 20);
            Require(status == CitedAnswerStatus.InsufficientEvidence ? proposal.Claims.Count == 0 : proposal.Claims.Count > 0);
            Require(status == CitedAnswerStatus.Answered ? proposal.ReasonCode is null :
                proposal.ReasonCode is not null && Reasons.Contains(proposal.ReasonCode));
            Require(status == CitedAnswerStatus.PartiallyAnswered ? proposal.UnansweredParts!.Count > 0 :
                proposal.UnansweredParts!.Count == 0);
            Require(proposal.UnansweredParts!.All(x => BoundedText(x, 1000)) &&
                proposal.UnansweredParts.Distinct(StringComparer.Ordinal).Count() == proposal.UnansweredParts.Count);
            Require(proposal.ReasonCode != "no_relevant_evidence" ||
                (status == CitedAnswerStatus.InsufficientEvidence && run.Hits.Count == 0));

            Require(retrievedChunks.Count == run.Hits.Count &&
                retrievedChunks.Select(x => x.Id).Distinct().Count() == retrievedChunks.Count);
            var chunks = retrievedChunks.ToDictionary(x => x.Id);
            foreach (var hit in run.Hits)
            {
                Require(hit.OrganizationId == run.OrganizationId && hit.RetrievalRunId == run.Id &&
                    run.Sources.Any(x => x.OrganizationId == run.OrganizationId && x.IndexId == hit.IndexId));
                Require(chunks.TryGetValue(hit.ChunkId, out var chunk) &&
                    chunk.OrganizationId == run.OrganizationId && chunk.IndexId == hit.IndexId);
                Require(Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(chunk!.Content)))
                    .Equals(chunk.ContentSha256, StringComparison.OrdinalIgnoreCase));
            }
            var claims = new List<StructurallyValidatedClaim>();
            var texts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var claim in proposal.Claims)
            {
                Require(claim is not null && BoundedText(claim.Text, 4000) && texts.Add(claim.Text));
                Require(claim!.Citations is not null && claim.Citations.Count is >= 1 and <= 8);
                var references = new HashSet<(Guid, Guid?)>();
                var citations = new List<VerifiedCitation>();
                foreach (var reference in claim.Citations!)
                {
                    Require(reference is not null && reference.HitId != Guid.Empty && reference.SpanId != Guid.Empty);
                    Require(references.Add((reference!.HitId, reference.SpanId)));
                    var hit = run.Hits.SingleOrDefault(x => x.Id == reference.HitId);
                    Require(hit is not null);
                    var chunk = chunks[hit!.ChunkId];
                    ChunkEvidenceSpan? span = null;
                    if (reference.SpanId.HasValue)
                    {
                        span = chunk.Spans.SingleOrDefault(x => x.Id == reference.SpanId);
                        Require(span is not null && span.OrganizationId == run.OrganizationId &&
                            span.ChunkId == chunk.Id && span.ExtractionRunId == chunk.ExtractionRunId &&
                            span.PageNumber > 0 && span.ByteStart >= 0 && span.ByteEnd > span.ByteStart &&
                            StrictUtf8.GetByteCount(span.Quote) == span.ByteEnd - span.ByteStart);
                        if (chunk.Kind == DocumentChunkKind.PageText) Require(span!.Quote == chunk.Content);
                    }
                    else Require(chunk.Kind == DocumentChunkKind.ReviewedFact && chunk.ReviewActionId.HasValue);
                    if (requireSupportText)
                    {
                        // Exact ordinal membership, not paraphrase, normalization, entailment or coverage.
                        // A corrected review value must bind to reviewed content, not its old machine quote.
                        var support = chunk.Kind == DocumentChunkKind.ReviewedFact ? chunk.Content : span!.Quote;
                        Require(BoundedText(reference.SupportText, 1000) &&
                            support.Contains(reference.SupportText!, StringComparison.Ordinal));
                    }
                    citations.Add(new(hit.Id, span?.Id, chunk.Id, chunk.IndexId, chunk.ReviewSessionId,
                        chunk.ExtractionRunId, chunk.Kind.ToString(), chunk.ReviewActionId,
                        span is null ? null : new(span.Id, span.PageNumber, span.ByteStart, span.ByteEnd,
                            span.Quote, span.BoundingPolygon, span.CoordinateUnit)) { SupportText = reference.SupportText });
                }
                claims.Add(new(claim.Text, citations.AsReadOnly()));
            }
            return new(requireSupportText ? SourceVersion : Version, status, proposal.ReasonCode, claims.AsReadOnly(),
                Array.AsReadOnly(proposal.UnansweredParts.ToArray()));
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            // Never expose malformed provider text, source text, or parser diagnostics.
            throw new CitedWorkflowException("invalid_answer_proposal", "The answer proposal failed citation validation.", 502);
        }
    }

    private static bool BoundedText(string? text, int maximum) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && text == text.Trim() &&
        !text.Any(c => char.IsControl(c) && c is not ('\n' or '\t'));

    private static void Require(bool valid)
    {
        if (!valid) throw new ArgumentException();
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name));
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateProperties(child);
    }
}

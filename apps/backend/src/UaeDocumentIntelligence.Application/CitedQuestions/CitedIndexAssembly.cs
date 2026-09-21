using System.Text;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record ChunkingProfile(int MaxBytes = 2048, int OverlapBytes = 256)
{
    public string Name { get; init; } = "page-lines";
    public string Version { get; init; } = FormattableString.Invariant($"1.0.0-b{MaxBytes}-o{OverlapBytes}");
    public void Validate()
    {
        if (MaxBytes is < 32 or > 65536 || OverlapBytes < 0 || OverlapBytes > MaxBytes / 4 ||
            Name != "page-lines" || Version != FormattableString.Invariant($"1.0.0-b{MaxBytes}-o{OverlapBytes}"))
            throw new ArgumentException("Invalid chunking profile limits.");
    }
}

public sealed record ChunkingPage(int PageNumber, string CanonicalText);
public sealed record ChunkingEvidence(Guid Id, int PageNumber, int? ByteStart, int? ByteEnd,
    string? Quote);
public sealed record ChunkingFact(string FieldKey, int OccurrenceIndex, Guid ReviewActionId,
    string Action, string? Value, IReadOnlyList<ChunkingEvidence> Evidence);
public sealed record ChunkingSnapshot(string SchemaVersion, Guid IndexId, Guid OrganizationId,
    Guid DocumentId, Guid ExtractionRunId, Guid ReviewSessionId, string ReviewStatus,
    string DocumentSha256, ChunkingProfile Profile, IReadOnlyList<ChunkingPage> Pages,
    IReadOnlyList<ChunkingFact> Facts);
public sealed record ChunkProposal(Guid Id, int Ordinal, string Kind, string Content,
    string ContentSha256, int? PageNumber, int? ByteStart, int? ByteEnd, string? FieldKey,
    int? OccurrenceIndex, Guid? ReviewActionId, IReadOnlyList<Guid> EvidenceIds);
public sealed record ChunkingResult(string SchemaVersion, Guid IndexId, Guid OrganizationId,
    Guid DocumentId, Guid ExtractionRunId, Guid ReviewSessionId, string DocumentSha256,
    ChunkingProfile Profile, IReadOnlyList<ChunkProposal> Chunks);

/// <summary>Trust boundary: reconstruct proposed chunks from authoritative domain sources.</summary>
public static class CitedIndexAssembly
{
    public static ChunkingSnapshot CreateSnapshot(Guid indexId, Document document,
        ReviewSession review, ExtractionRun extraction, ChunkingProfile profile)
    {
        profile.Validate();
        // Constructor checks organization, document and completed review eligibility.
        _ = new CitedDocumentIndex(indexId, document, review, profile.Name, profile.Version, DateTimeOffset.UtcNow);
        if (extraction.Id != review.ExtractionRunId || extraction.OrganizationId != document.OrganizationId ||
            extraction.DocumentId != document.Id)
            throw new ArgumentException("Extraction is outside the pinned review.");
        if (extraction.Pages.Count > 2000 ||
            extraction.Pages.Sum(x => (long)Encoding.UTF8.GetByteCount(x.CanonicalText)) > 8388608)
            throw new ArgumentException("Source exceeds indexing limits.");
        var facts = review.Actions.GroupBy(x => (x.FieldKey, x.OccurrenceIndex))
            .Select(group => group.MaxBy(x => x.SequenceNumber)!)
            .OrderBy(x => x.FieldKey, StringComparer.Ordinal).ThenBy(x => x.OccurrenceIndex)
            .Select(action =>
            {
                var machine = action.MachineFieldValueId.HasValue
                    ? extraction.Fields.Single(x => x.Id == action.MachineFieldValueId) : null;
                var value = action.Action switch
                {
                    ReviewActionType.Cleared => null,
                    ReviewActionType.Confirmed => machine!.NormalizedValueText ?? machine.ValueText,
                    _ => action.CorrectedValueText
                };
                var evidence = (machine?.Evidence ?? Array.Empty<FieldEvidence>()).Select(x =>
                {
                    var textBacked = x.ByteStart.HasValue && x.ByteEnd > x.ByteStart;
                    return new ChunkingEvidence(x.Id, x.PageNumber, textBacked ? x.ByteStart : null,
                        textBacked ? x.ByteEnd : null, textBacked ? x.QuotedText : null);
                }).OrderBy(x => x.Id.ToString(), StringComparer.Ordinal).ToArray();
                if (evidence.Length > 1000) throw new ArgumentException("Too many field evidence references.");
                return new ChunkingFact(action.FieldKey, action.OccurrenceIndex, action.Id,
                    action.Action.ToString(), value, evidence);
            }).ToArray();
        if (facts.Length > 5000) throw new ArgumentException("Too many reviewed facts.");
        return new("1.0", indexId, document.OrganizationId, document.Id, extraction.Id,
            review.Id, review.Status.ToString(), document.Sha256, profile,
            extraction.Pages.OrderBy(x => x.PageNumber)
                .Select(x => new ChunkingPage(x.PageNumber, x.CanonicalText)).ToArray(), facts);
    }

    public static CitedDocumentIndex Assemble(Guid indexId, Document document, ReviewSession review,
        ExtractionRun extraction, ChunkingProfile profile, ChunkingResult result, DateTimeOffset now)
    {
        var expected = CreateSnapshot(indexId, document, review, extraction, profile);
        if (result is null || result.SchemaVersion != expected.SchemaVersion || result.IndexId != indexId ||
            result.OrganizationId != expected.OrganizationId || result.DocumentId != document.Id ||
            result.ExtractionRunId != extraction.Id || result.ReviewSessionId != review.Id ||
            result.DocumentSha256 != document.Sha256 || result.Profile != profile ||
            result.Chunks is null || result.Chunks.Count is < 1 or > 10000)
            throw new ArgumentException("Chunking result does not match the authorized source/profile.");
        var index = new CitedDocumentIndex(indexId, document, review, profile.Name, profile.Version, now);
        var pages = extraction.Pages.ToDictionary(x => x.PageNumber);
        var coverage = pages.ToDictionary(x => x.Key, _ => (Start: -1, End: 0));
        var facts = expected.Facts.Where(x => x.Action != "Cleared")
            .ToDictionary(x => (x.FieldKey, x.OccurrenceIndex));
        var seenFacts = new HashSet<(string, int)>();
        var factsStarted = false;
        var lastPage = 0;
        foreach (var proposed in result.Chunks)
        {
            if (proposed is null || proposed.Ordinal != index.Chunks.Count ||
                string.IsNullOrWhiteSpace(proposed.Content) ||
                Encoding.UTF8.GetByteCount(proposed.Content) > profile.MaxBytes || proposed.EvidenceIds is null)
                throw new ArgumentException("Malformed or oversized chunk proposal.");
            DocumentChunk actual;
            if (proposed.Kind == "PageText")
            {
                if (factsStarted || proposed.PageNumber is not int number || number < lastPage ||
                    !pages.TryGetValue(number, out var page) || proposed.ByteStart is not int start ||
                    proposed.ByteEnd is not int end || proposed.FieldKey is not null ||
                    proposed.OccurrenceIndex is not null || proposed.ReviewActionId is not null ||
                    proposed.EvidenceIds.Count != 0)
                    throw new ArgumentException("Invalid page chunk source or ordering.");
                var previous = coverage[number];
                if (start <= previous.Start || end <= previous.End ||
                    previous.End - start > profile.OverlapBytes ||
                    (start > previous.End && !WhitespaceGap(page.CanonicalText, previous.End, start)))
                    throw new ArgumentException("Page coverage has a gap, excessive overlap, or no progress.");
                actual = index.AddPageChunk(proposed.Id, proposed.Ordinal, page, start, end);
                coverage[number] = (start, end);
                lastPage = number;
            }
            else if (proposed.Kind == "ReviewedFact")
            {
                factsStarted = true;
                if (proposed.FieldKey is null || proposed.OccurrenceIndex is not int occurrence ||
                    !facts.TryGetValue((proposed.FieldKey, occurrence), out var fact) ||
                    !seenFacts.Add((proposed.FieldKey, occurrence)) || proposed.ReviewActionId != fact.ReviewActionId ||
                    proposed.PageNumber is not null || proposed.ByteStart is not null || proposed.ByteEnd is not null ||
                    !proposed.EvidenceIds.SequenceEqual(fact.Evidence.Select(x => x.Id)))
                    throw new ArgumentException("Invalid reviewed fact or evidence references.");
                actual = index.AddReviewedFact(proposed.Id, proposed.Ordinal, review,
                    proposed.FieldKey, occurrence, extraction);
            }
            else throw new ArgumentException("Unknown chunk kind.");
            if (actual.Content != proposed.Content || actual.ContentSha256 != proposed.ContentSha256 ||
                actual.Id != CitedChunkIdentity.Chunk(indexId, actual.Ordinal, actual.Kind, actual.ContentSha256))
                throw new ArgumentException("Chunk ID, text or content hash does not match authoritative evidence.");
        }
        foreach (var page in pages.Values)
            if (!WhitespaceGap(page.CanonicalText, coverage[page.PageNumber].End,
                    Encoding.UTF8.GetByteCount(page.CanonicalText)))
                throw new ArgumentException("Chunking omitted source text.");
        if (seenFacts.Count != facts.Count) throw new ArgumentException("Chunking omitted reviewed facts.");
        index.Publish();
        return index;
    }

    private static bool WhitespaceGap(string text, int start, int end)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (start < 0 || end < start || end > bytes.Length) return false;
        try { return string.IsNullOrWhiteSpace(new UTF8Encoding(false, true).GetString(bytes, start, end - start)); }
        catch (DecoderFallbackException) { return false; }
    }
}

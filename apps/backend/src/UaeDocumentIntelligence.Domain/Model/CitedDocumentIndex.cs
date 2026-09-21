using System.Security.Cryptography;
using System.Text;
using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public enum DocumentIndexStatus { Building, Ready, Superseded, Quarantined }
public enum DocumentChunkKind { PageText, ReviewedFact }

/// <summary>A versioned index of one completed review; content stays frozen after publication.</summary>
public sealed class CitedDocumentIndex
{
    private readonly List<DocumentChunk> _chunks = [];
    private CitedDocumentIndex() { }

    public CitedDocumentIndex(Guid id, Document document, ReviewSession review,
        string profileName, string profileVersion, DateTimeOffset now)
    {
        CitedGuard.Id(id);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(review);
        if (review.OrganizationId != document.OrganizationId || review.DocumentId != document.Id ||
            review.Status != ReviewStatus.Completed)
            throw new ArgumentException("Index requires a completed review of this document.");
        Id = id;
        OrganizationId = document.OrganizationId;
        DocumentId = document.Id;
        ReviewSessionId = review.Id;
        ExtractionRunId = review.ExtractionRunId;
        Filename = document.OriginalFilename;
        DocumentSha256 = document.Sha256;
        ProfileName = Guard.Required(profileName, nameof(profileName), 100);
        ProfileVersion = Guard.Required(profileVersion, nameof(profileVersion), 100);
        CreatedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid ReviewSessionId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public string Filename { get; private set; } = string.Empty;
    public string DocumentSha256 { get; private set; } = string.Empty;
    public string ProfileName { get; private set; } = string.Empty;
    public string ProfileVersion { get; private set; } = string.Empty;
    public DocumentIndexStatus Status { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<DocumentChunk> Chunks => _chunks.AsReadOnly();

    public DocumentChunk AddPageChunk(Guid id, int ordinal, ExtractionPage page,
        int byteStart, int byteEnd)
    {
        RequireBuilding(id, ordinal);
        RequirePage(page);
        var text = CitedGuard.Slice(page.CanonicalText, byteStart, byteEnd);
        var chunk = new DocumentChunk(id, this, ordinal, DocumentChunkKind.PageText, text, null);
        chunk.AddSpan(new ChunkEvidenceSpan(CitedChunkIdentity.Span(id, page.PageNumber, byteStart, byteEnd, null),
            chunk, page, byteStart, byteEnd, null));
        _chunks.Add(chunk);
        ConcurrencyToken = Guid.NewGuid();
        return chunk;
    }

    public DocumentChunk AddReviewedFact(Guid id, int ordinal, ReviewSession review,
        string fieldKey, int occurrence, ExtractionRun extraction)
    {
        RequireBuilding(id, ordinal);
        if (review.Id != ReviewSessionId || review.OrganizationId != OrganizationId ||
            review.Status != ReviewStatus.Completed || extraction.Id != ExtractionRunId ||
            extraction.OrganizationId != OrganizationId || extraction.DocumentId != DocumentId)
            throw new ArgumentException("Reviewed fact must use the pinned completed review and run.");
        var action = review.Actions.Where(x => x.FieldKey == fieldKey && x.OccurrenceIndex == occurrence)
            .OrderByDescending(x => x.SequenceNumber).FirstOrDefault()
            ?? throw new ArgumentException("No decision exists for this reviewed field.");
        if (action.Action == ReviewActionType.Cleared)
            throw new ArgumentException("Cleared fields cannot become reviewed facts.");
        var machine = action.MachineFieldValueId.HasValue
            ? extraction.Fields.Single(x => x.Id == action.MachineFieldValueId.Value) : null;
        var value = action.Action == ReviewActionType.Confirmed
            ? machine!.NormalizedValueText ?? machine.ValueText : action.CorrectedValueText!;
        var chunk = new DocumentChunk(id, this, ordinal, DocumentChunkKind.ReviewedFact,
            $"{fieldKey}[{occurrence}]: {value}", action.Id);
        foreach (var evidence in (machine?.Evidence ?? Array.Empty<FieldEvidence>())
                     .OrderBy(x => x.Id.ToString("N"), StringComparer.Ordinal))
        {
            var page = extraction.Pages.Single(x => x.PageNumber == evidence.PageNumber);
            // Preserve the machine quote as original evidence; it is not a quote of the correction.
            if (evidence.ByteStart.HasValue && evidence.ByteEnd.HasValue &&
                evidence.ByteEnd > evidence.ByteStart)
                chunk.AddSpan(new ChunkEvidenceSpan(CitedChunkIdentity.Span(id, page.PageNumber,
                    evidence.ByteStart.Value, evidence.ByteEnd.Value, evidence.Id), chunk, page,
                    evidence.ByteStart.Value, evidence.ByteEnd.Value, evidence));
        }
        _chunks.Add(chunk);
        ConcurrencyToken = Guid.NewGuid();
        return chunk;
    }

    public void Publish()
    {
        if (Status != DocumentIndexStatus.Building || _chunks.Count == 0)
            throw new InvalidOperationException("Only a nonempty building index can be published.");
        Status = DocumentIndexStatus.Ready;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Retire(bool quarantine = false)
    {
        if (Status != DocumentIndexStatus.Ready)
            throw new InvalidOperationException("Only ready indexes can be retired.");
        Status = quarantine ? DocumentIndexStatus.Quarantined : DocumentIndexStatus.Superseded;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void RequireBuilding(Guid id, int ordinal)
    {
        CitedGuard.Id(id);
        if (Status != DocumentIndexStatus.Building)
            throw new InvalidOperationException("Published index content is immutable.");
        if (ordinal < 0 || _chunks.Any(x => x.Id == id || x.Ordinal == ordinal))
            throw new ArgumentException("Chunk ID and nonnegative ordinal must be unique in the index.");
    }

    private void RequirePage(ExtractionPage page)
    {
        if (page.OrganizationId != OrganizationId || page.ExtractionRunId != ExtractionRunId)
            throw new ArgumentException("Page must belong to the pinned extraction run.");
    }
}

public sealed class DocumentChunk
{
    private readonly List<ChunkEvidenceSpan> _spans = [];
    private DocumentChunk() { }
    internal DocumentChunk(Guid id, CitedDocumentIndex index, int ordinal, DocumentChunkKind kind,
        string content, Guid? reviewActionId)
    {
        if (string.IsNullOrWhiteSpace(content) || Encoding.UTF8.GetByteCount(content) > 65536)
            throw new ArgumentException("Chunk text must be nonempty and at most 65536 UTF-8 bytes.");
        Id = id;
        OrganizationId = index.OrganizationId;
        IndexId = index.Id;
        ExtractionRunId = index.ExtractionRunId;
        ReviewSessionId = index.ReviewSessionId;
        Ordinal = ordinal;
        Kind = kind;
        Content = content;
        ContentSha256 = CitedGuard.Hash(content);
        ReviewActionId = reviewActionId;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid IndexId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public Guid ReviewSessionId { get; private set; }
    public int Ordinal { get; private set; }
    public DocumentChunkKind Kind { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string ContentSha256 { get; private set; } = string.Empty;
    public Guid? ReviewActionId { get; private set; }
    public IReadOnlyCollection<ChunkEvidenceSpan> Spans => _spans.AsReadOnly();
    internal void AddSpan(ChunkEvidenceSpan span) => _spans.Add(span);
}

public sealed class ChunkEvidenceSpan
{
    private ChunkEvidenceSpan() { }
    internal ChunkEvidenceSpan(Guid id, DocumentChunk chunk, ExtractionPage page,
        int byteStart, int byteEnd, FieldEvidence? evidence)
    {
        Id = id;
        OrganizationId = chunk.OrganizationId;
        ChunkId = chunk.Id;
        ExtractionRunId = chunk.ExtractionRunId;
        PageNumber = page.PageNumber;
        ByteStart = byteStart;
        ByteEnd = byteEnd;
        Quote = CitedGuard.Slice(page.CanonicalText, byteStart, byteEnd);
        FieldEvidenceId = evidence?.Id;
        BoundingPolygon = evidence?.BoundingPolygon;
        CoordinateUnit = evidence?.CoordinateUnit;
    }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ChunkId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public int PageNumber { get; private set; }
    public int ByteStart { get; private set; }
    public int ByteEnd { get; private set; }
    public string Quote { get; private set; } = string.Empty;
    public Guid? FieldEvidenceId { get; private set; }
    public string? BoundingPolygon { get; private set; }
    public string? CoordinateUnit { get; private set; }
}

internal static class CitedGuard
{
    internal static void Id(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Nonempty identity is required.");
    }
    internal static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    internal static string Slice(string text, int start, int end)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (start < 0 || end <= start || end > bytes.Length)
            throw new ArgumentException("Invalid nonempty UTF-8 byte span.");
        try { return new UTF8Encoding(false, true).GetString(bytes.AsSpan(start, end - start)); }
        catch (DecoderFallbackException ex) { throw new ArgumentException("Span splits a UTF-8 character.", ex); }
    }
}

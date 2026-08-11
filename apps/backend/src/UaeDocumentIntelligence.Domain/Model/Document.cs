using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed partial class Document
{
    private readonly List<ExtractionRun> _extractionRuns = [];
    private readonly List<ReviewSession> _reviewSessions = [];
    private Document() { }

    public Document(Guid id, Guid organizationId, Guid createdByActorId, string originalFilename,
        string storageKey, string declaredContentType, long sizeBytes, string sha256, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || createdByActorId == Guid.Empty)
            throw new ArgumentException("Document, organization, and actor IDs are required.");
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (!Sha256Pattern().IsMatch(sha256)) throw new ArgumentException("SHA-256 must be 64 lowercase hexadecimal characters.", nameof(sha256));
        Id = id;
        OrganizationId = organizationId;
        CreatedByActorId = createdByActorId;
        OriginalFilename = Guard.Required(originalFilename, nameof(originalFilename), 255);
        StorageKey = Guard.Required(storageKey, nameof(storageKey), 255);
        DeclaredContentType = Guard.Required(declaredContentType, nameof(declaredContentType), 100);
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        ProcessingStatus = DocumentProcessingStatus.Stored;
        CreatedAtUtc = UpdatedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CreatedByActorId { get; private set; }
    public string OriginalFilename { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string DeclaredContentType { get; private set; } = string.Empty;
    public string? DetectedContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DocumentProcessingStatus ProcessingStatus { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<ExtractionRun> ExtractionRuns => _extractionRuns.AsReadOnly();
    public IReadOnlyCollection<ReviewSession> ReviewSessions => _reviewSessions.AsReadOnly();

    public ExtractionRun QueueExtraction(Guid runId, int runNumber, string providerName,
        string providerVersion, string schemaVersion, string correlationId, DateTimeOffset now)
    {
        if (_extractionRuns.Any(x => x.RunNumber == runNumber)) throw new InvalidOperationException("Run number already exists.");
        var run = new ExtractionRun(runId, OrganizationId, Id, runNumber, providerName,
            providerVersion, schemaVersion, correlationId, now);
        _extractionRuns.Add(run);
        if (ProcessingStatus != DocumentProcessingStatus.ReadyForReview)
            ProcessingStatus = DocumentProcessingStatus.Processing;
        Touch(now);
        return run;
    }

    public void RecordExtractionSuccess(ExtractionRun run, DateTimeOffset now)
    {
        RequireOwnedRun(run);
        if (run.Status is not (ExtractionRunStatus.Succeeded or ExtractionRunStatus.Partial))
            throw new InvalidOperationException("Run is not reviewable.");
        ProcessingStatus = DocumentProcessingStatus.ReadyForReview;
        Touch(now);
    }

    public void RecordExtractionFailure(ExtractionRun run, DateTimeOffset now)
    {
        RequireOwnedRun(run);
        if (run.Status is not (ExtractionRunStatus.Failed or ExtractionRunStatus.Cancelled))
            throw new InvalidOperationException("Run is not a failed terminal attempt.");
        if (!_extractionRuns.Any(IsReviewable))
            ProcessingStatus = run.Status == ExtractionRunStatus.Cancelled
                ? DocumentProcessingStatus.ProcessingCancelled
                : DocumentProcessingStatus.ExtractionFailed;
        Touch(now);
    }

    public ExtractionRun? SelectedExtractionRun() => _extractionRuns
        .Where(IsReviewable)
        .OrderByDescending(x => x.RunNumber)
        .FirstOrDefault();

    public ReviewSession OpenReview(Guid reviewSessionId, Guid actorId, DateTimeOffset now)
    {
        if (reviewSessionId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Review session and actor IDs are required.");
        var selectedRun = SelectedExtractionRun()
            ?? throw new InvalidOperationException("A successful extraction is required before review.");
        if (_reviewSessions.Any(x => x.Status is ReviewStatus.Pending or ReviewStatus.InProgress))
            throw new InvalidOperationException("An active review already exists.");
        if (_reviewSessions.Any(x => x.Status == ReviewStatus.Completed && x.ExtractionRunId == selectedRun.Id))
            throw new InvalidOperationException("The selected extraction run has already been reviewed.");

        var revision = _reviewSessions.Select(x => x.RevisionNumber).DefaultIfEmpty(0).Max() + 1;
        var superseded = _reviewSessions.Where(x => x.Status == ReviewStatus.Completed)
            .OrderByDescending(x => x.RevisionNumber).FirstOrDefault();
        var session = new ReviewSession(reviewSessionId, OrganizationId, Id, selectedRun.Id,
            revision, now, actorId, superseded?.Id);
        session.Start(now);
        _reviewSessions.Add(session);
        Touch(now);
        return session;
    }

    private void RequireOwnedRun(ExtractionRun run)
    {
        if (!_extractionRuns.Contains(run)) throw new InvalidOperationException("Extraction run does not belong to document.");
    }

    private static bool IsReviewable(ExtractionRun run) =>
        run.Status is ExtractionRunStatus.Succeeded or ExtractionRunStatus.Partial;

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}

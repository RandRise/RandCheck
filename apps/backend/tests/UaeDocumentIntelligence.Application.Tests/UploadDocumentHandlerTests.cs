using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Application.Documents.Processing;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class UploadDocumentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData("")]
    [InlineData(" bad ")]
    [InlineData("unsafe,key")]
    public async Task Invalid_operation_key_rejects_before_storage(string key)
    {
        var queue = new FakeUploadQueueStore(); var storage = new FakeDocumentStorage();
        var error = await Assert.ThrowsAsync<ProcessingIntentException>(() => CreateHandler(queue, storage)
            .HandleAsync(CreateCommand() with { RequestKey = key }, default));
        Assert.False(error.Conflict); Assert.Equal("invalid_operation_key", error.Code);
        Assert.Equal(0, storage.SaveCallCount); Assert.Equal(0, queue.AddCallCount);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10485761)]
    public async Task Keyed_upload_checks_exact_bounded_bytes_before_admission(long declared)
    {
        var queue = new FakeUploadQueueStore(); var storage = new FakeDocumentStorage();
        await Assert.ThrowsAsync<DocumentTooLargeException>(() => CreateHandler(queue, storage)
            .HandleAsync(CreateCommand() with { RequestKey = "intent", SizeBytes = declared }, default));
        Assert.Equal(0, storage.SaveCallCount); Assert.Equal(0, queue.AddCallCount);
    }

    [Fact]
    public async Task Keyed_replay_skips_storage_and_racing_replay_compensates_only_its_generated_source()
    {
        var originalQueue = new FakeUploadQueueStore(); var originalStorage = new FakeDocumentStorage();
        await CreateHandler(originalQueue, originalStorage).HandleAsync(CreateCommand(), default);
        var original = originalQueue.Document!;
        var replayQueue = new FakeUploadQueueStore { Replay = original };
        var replayStorage = new FakeDocumentStorage();
        var replay = await CreateHandler(replayQueue, replayStorage).HandleAsync(CreateCommand() with { RequestKey = "intent" }, default);
        Assert.Equal(original.Id, replay.Id); Assert.Equal(0, replayStorage.SaveCallCount);
        var raceQueue = new FakeUploadQueueStore { Accepted = original };
        var raceStorage = new FakeDocumentStorage { StoredFile = originalStorage.StoredFile with { StorageKey = "loser/source.pdf" } };
        replay = await CreateHandler(raceQueue, raceStorage).HandleAsync(CreateCommand() with { RequestKey = "intent" }, default);
        Assert.Equal(original.Id, replay.Id); Assert.Equal(new[] { "loser/source.pdf" }, raceStorage.DeletedStorageKeys);
        Assert.NotNull(raceQueue.Intent); Assert.Equal(ActorId, raceQueue.Intent.ActorId);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_preserves_owned_source_for_keyed_retry()
    {
        var queue = new FakeUploadQueueStore { CommitThenThrow = true };
        var storage = new FakeDocumentStorage();
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(queue, storage)
            .HandleAsync(CreateCommand() with { RequestKey = "intent" }, default));
        Assert.Empty(storage.DeletedStorageKeys);
        var replay = await CreateHandler(queue, storage).HandleAsync(CreateCommand() with { RequestKey = "intent" }, default);
        Assert.Equal(queue.Document!.Id, replay.Id); Assert.Equal(1, storage.SaveCallCount);
    }

    [Fact]
    public async Task Uncertain_compensation_lookup_preserves_source_for_existing_orphan_recovery()
    {
        var queue = new FakeUploadQueueStore { LookupFailsAfterAdd = true, Exception = new InvalidOperationException("Uncertain commit") };
        var storage = new FakeDocumentStorage();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(queue, storage)
            .HandleAsync(CreateCommand() with { RequestKey = "intent" }, default));
        Assert.Equal("Uncertain commit", error.Message);
        Assert.Empty(storage.DeletedStorageKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scan_receipt_is_forwarded_and_failed_admission_compensates_source(bool fail)
    {
        var storage = new FakeDocumentStorage();
        var receipt = new FileScanReceipt(storage.StoredFile.Sha256, 4, "clamav-instream-v1");
        storage.StoredFile = storage.StoredFile with { Scan = receipt };
        var queue = new FakeUploadQueueStore { Exception = fail ? new InvalidOperationException("Admission failed") : null };
        if (fail) await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler(queue, storage).HandleAsync(CreateCommand(), default));
        else await CreateHandler(queue, storage).HandleAsync(CreateCommand(), default);
        Assert.Same(receipt, queue.Scan);
        Assert.Equal(fail ? 1 : 0, storage.DeletedStorageKeys.Count);
    }

    [Fact]
    public async Task Queue_persistence_failure_deletes_the_stored_file_with_a_compensation_token()
    {
        using var requestCancellation = new CancellationTokenSource();
        var queue = new FakeUploadQueueStore
        {
            BeforeFailure = requestCancellation.Cancel,
            Exception = new InvalidOperationException("Synthetic database failure.")
        };
        var storage = new FakeDocumentStorage();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler(queue, storage).HandleAsync(CreateCommand(), requestCancellation.Token));

        Assert.Equal("Synthetic database failure.", exception.Message);
        Assert.Single(storage.DeletedStorageKeys);
        Assert.Equal(storage.StoredFile.StorageKey, storage.DeletedStorageKeys[0]);
        Assert.Single(storage.DeleteTokens);
        Assert.NotEqual(requestCancellation.Token, storage.DeleteTokens[0]);
        Assert.False(storage.DeleteTokens[0].IsCancellationRequested);
    }

    [Fact]
    public async Task Successful_upload_atomically_queues_document_run_and_job_without_calling_provider()
    {
        var queue = new FakeUploadQueueStore();
        var storage = new FakeDocumentStorage();
        var extraction = new FakeExtractionClient();

        var result = await CreateHandler(queue, storage, extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Processing", result.Status);
        Assert.Null(result.DocumentType);
        Assert.Empty(result.Fields);
        Assert.Empty(storage.DeletedStorageKeys);
        Assert.Equal(0, extraction.ExtractCallCount);
        Assert.Equal(1, queue.AddCallCount);
        Assert.NotNull(queue.Document);
        Assert.NotNull(queue.Job);
        Assert.Equal(result.Id, queue.Document.Id);
        Assert.Equal(OrganizationId, queue.Document.OrganizationId);
        Assert.Equal(queue.Document.Id, queue.Job.DocumentId);
        var run = Assert.Single(queue.Document.ExtractionRuns);
        Assert.Equal(ExtractionRunStatus.Queued, run.Status);
        Assert.Equal(run.Id, queue.Job.ExtractionRunId);
        Assert.Equal(ExtractionJobStatus.Pending, queue.Job.Status);
        Assert.Equal("local", run.ProviderName);
        Assert.Equal("local-1.0.0", run.ProviderVersion);
        Assert.Equal("2.0", run.ExtractionSchemaVersion);
        Assert.Equal("request-123", run.CorrelationId);
    }

    [Fact]
    public async Task Storage_receives_tenant_and_the_same_document_id_that_is_persisted()
    {
        var queue = new FakeUploadQueueStore();
        var storage = new FakeDocumentStorage();

        await CreateHandler(queue, storage).HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal(OrganizationId, storage.OrganizationId);
        Assert.Equal(queue.Document!.Id, storage.DocumentId);
        Assert.Equal(4, storage.DeclaredSize);
        Assert.Equal("invoice.pdf", storage.OriginalFilename);
    }

    [Fact]
    public async Task Oversized_correlation_id_is_rejected_before_storage_or_queue_persistence()
    {
        var queue = new FakeUploadQueueStore();
        var storage = new FakeDocumentStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => CreateHandler(queue, storage)
            .HandleAsync(CreateCommand(new string('x', 101)), CancellationToken.None));

        Assert.Equal(0, storage.SaveCallCount);
        Assert.Equal(0, queue.AddCallCount);
    }

    [Fact]
    public async Task Unsupported_type_is_rejected_before_storage_or_queue_persistence()
    {
        var queue = new FakeUploadQueueStore();
        var storage = new FakeDocumentStorage();
        var command = CreateCommand() with { ContentType = "application/octet-stream" };

        await Assert.ThrowsAsync<UnsupportedDocumentTypeException>(() => CreateHandler(queue, storage)
            .HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, storage.SaveCallCount);
        Assert.Equal(0, queue.AddCallCount);
    }

    private static UploadDocumentHandler CreateHandler(FakeUploadQueueStore queue,
        FakeDocumentStorage storage, FakeExtractionClient? extraction = null) =>
        new(queue, storage, extraction ?? new FakeExtractionClient(),
            new FakeExecutionContext(OrganizationId, ActorId), new FakeClock(Now));

    private static UploadDocumentCommand CreateCommand(string correlationId = "request-123") =>
        new("invoice.pdf", "application/pdf", 4, new MemoryStream([1, 2, 3, 4]), correlationId);

    private sealed class FakeUploadQueueStore : IUploadQueueStore
    {
        public Document? Replay { get; init; }
        public Document? Accepted { get; init; }
        public bool CommitThenThrow { get; init; }
        public bool LookupFailsAfterAdd { get; init; }
        public ProcessingIntent? Intent { get; private set; }
        public Task<Document?> FindUploadAsync(Guid org, ProcessingIntent intent, CancellationToken ct)
        { Intent = intent; if (LookupFailsAfterAdd && AddCallCount > 0) throw new InvalidOperationException("Lookup unavailable"); return Task.FromResult(CommitThenThrow ? Document : Replay); }
        public async Task<Document> AddWithIntentAsync(Document document, Guid run, Guid job, FileScanReceipt? scan, ProcessingIntent intent, CancellationToken ct)
        { Intent = intent; await AddAsync(document, run, job, ct); if (CommitThenThrow) throw new InvalidOperationException("Commit acknowledgement lost"); return Accepted ?? document; }
        public Exception? Exception { get; init; }
        public Action? BeforeFailure { get; init; }
        public int AddCallCount { get; private set; }
        public Document? Document { get; private set; }
        public ExtractionJob? Job { get; private set; }
        public FileScanReceipt? Scan { get; private set; }
        public Task<ExtractionJob> AddScannedAsync(Document document, Guid extractionRunId, Guid jobId, FileScanReceipt scan, CancellationToken ct)
        {
            Scan = scan;
            return AddAsync(document, extractionRunId, jobId, ct);
        }

        public Task<ExtractionJob> AddAsync(Document document, Guid extractionRunId, Guid jobId,
            CancellationToken cancellationToken)
        {
            AddCallCount++;
            Document = document;
            Job = new ExtractionJob(jobId, document.OrganizationId, document.Id, extractionRunId, Now);
            if (Exception is null) return Task.FromResult(Job);
            BeforeFailure?.Invoke();
            return Task.FromException<ExtractionJob>(Exception);
        }
    }

    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        public StoredFile StoredFile { get; set; } = new(
            "organizations/test/documents/test/source.pdf",
            @"C:\safe-test-storage\source.pdf",
            4,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

        public int SaveCallCount { get; private set; }
        public string? OriginalFilename { get; private set; }
        public long? DeclaredSize { get; private set; }
        public Guid? OrganizationId { get; private set; }
        public Guid? DocumentId { get; private set; }
        public List<string> DeletedStorageKeys { get; } = [];
        public List<CancellationToken> DeleteTokens { get; } = [];

        public Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
            Guid organizationId, Guid documentId, CancellationToken cancellationToken)
        {
            SaveCallCount++;
            OriginalFilename = originalFilename;
            DeclaredSize = declaredSize;
            OrganizationId = organizationId;
            DocumentId = documentId;
            return Task.FromResult(StoredFile);
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            DeletedStorageKeys.Add(storageKey);
            DeleteTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExtractionClient : IExtractionClient
    {
        public string ProviderName => "local";
        public string ProviderVersion => "local-1.0.0";
        public string SchemaVersion => "2.0";
        public int ExtractCallCount { get; private set; }

        public Task<ExtractionResult> ExtractAsync(WorkerExtractionRequest request,
            CancellationToken cancellationToken)
        {
            ExtractCallCount++;
            throw new InvalidOperationException("Upload must not invoke the extraction provider.");
        }
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed record FakeClock(DateTimeOffset UtcNow) : IClock;
}

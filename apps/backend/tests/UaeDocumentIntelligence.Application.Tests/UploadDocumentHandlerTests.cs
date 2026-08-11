using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Upload;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class UploadDocumentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pre_persistence_failure_deletes_newly_stored_file()
    {
        var repository = new FakeDocumentRepository { FailOnSaveCall = 1 };
        var storage = new FakeDocumentStorage();
        var handler = CreateHandler(repository, storage, new FakeExtractionClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateCommand(), CancellationToken.None));

        Assert.Equal("Persistence failure on save 1.", exception.Message);
        Assert.Equal(1, storage.SaveCallCount);
        Assert.Equal(new[] { storage.StoredFile.StorageKey }, storage.DeletedStorageKeys);
    }

    [Fact]
    public async Task Extraction_finalization_failure_does_not_delete_file_already_referenced_by_database()
    {
        var repository = new FakeDocumentRepository { FailOnSaveCall = 2 };
        var storage = new FakeDocumentStorage();
        var handler = CreateHandler(repository, storage, new FakeExtractionClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateCommand(), CancellationToken.None));

        Assert.Equal("Persistence failure on save 2.", exception.Message);
        Assert.Equal(2, repository.SaveCallCount);
        Assert.NotNull(repository.AddedDocument);
        Assert.Empty(storage.DeletedStorageKeys);
    }

    [Fact]
    public async Task Extraction_provider_exception_returns_failed_result_with_safe_summary()
    {
        const string sensitiveMessage = @"Provider exposed C:\private\supplier\invoice.pdf and token secret-123";
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var extraction = new FakeExtractionClient
        {
            Exception = new InvalidOperationException(sensitiveMessage)
        };
        var handler = CreateHandler(repository, storage, extraction);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("The extraction provider could not process this document.", result.ErrorMessage);
        Assert.DoesNotContain(sensitiveMessage, result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-123", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(2, repository.SaveCallCount);
        Assert.Empty(storage.DeletedStorageKeys);
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal(ExtractionRunStatus.Failed, run.Status);
        Assert.Equal("extraction_failed", run.FailureCode);
    }

    [Fact]
    public async Task Successful_upload_returns_ready_output_with_extracted_field_and_evidence()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var extraction = new FakeExtractionClient
        {
            Result = new ExtractionResult("invoice", "en", "provider-returned-extractor-v7",
            [
                new ExtractionField("total", "105.00", 0.98m, 2)
            ])
        };
        var handler = CreateHandler(repository, storage, extraction);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Ready", result.Status);
        Assert.Equal("invoice", result.DocumentType);
        Assert.Equal("en", result.Language);
        Assert.Equal("provider-returned-extractor-v7", result.Extractor);
        Assert.Null(result.ErrorMessage);
        var field = Assert.Single(result.Fields);
        Assert.Equal("total", field.Name);
        Assert.Equal("105.00", field.Value);
        Assert.Equal(0.98m, field.Confidence);
        Assert.Equal(2, field.Page);
        Assert.Equal(2, repository.SaveCallCount);
        Assert.Empty(storage.DeletedStorageKeys);
    }

    [Fact]
    public async Task Oversized_correlation_id_is_rejected_before_storage_or_persistence()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var handler = CreateHandler(repository, storage, new FakeExtractionClient());
        var command = CreateCommand(correlationId: new string('x', 101));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(0, storage.SaveCallCount);
        Assert.Null(repository.AddedDocument);
        Assert.Equal(0, repository.SaveCallCount);
    }

    [Fact]
    public async Task Request_cancellation_terminalizes_persisted_run_and_uses_compensation_token()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var extraction = new FakeExtractionClient
        {
            BeforeCompletion = cancellation.Cancel,
            Exception = new OperationCanceledException(cancellation.Token)
        };
        var handler = CreateHandler(repository, storage, extraction);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(CreateCommand(), cancellation.Token));

        Assert.Equal(2, repository.SaveCallCount);
        Assert.Equal(cancellation.Token, repository.SaveTokens[0]);
        Assert.NotEqual(cancellation.Token, repository.SaveTokens[1]);
        Assert.False(repository.SaveTokenWasCancelledAtCall[0]);
        Assert.False(repository.SaveTokenWasCancelledAtCall[1]);
        Assert.True(repository.SaveTokens[0].IsCancellationRequested);
        Assert.False(repository.SaveTokens[1].IsCancellationRequested);
        Assert.Empty(storage.DeletedStorageKeys);
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal(ExtractionRunStatus.Cancelled, run.Status);
        Assert.Equal("request_cancelled", run.FailureCode);
        Assert.NotNull(run.CompletedAtUtc);
    }

    [Fact]
    public async Task Provider_timeout_becomes_terminal_failed_document_result()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var extraction = new FakeExtractionClient
        {
            Exception = new OperationCanceledException("Provider timeout without request cancellation.")
        };
        var handler = CreateHandler(repository, storage, extraction);

        var result = await handler.HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("The extraction provider timed out.", result.ErrorMessage);
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal(ExtractionRunStatus.Cancelled, run.Status);
        Assert.Equal("provider_timeout", run.FailureCode);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.Equal(2, repository.SaveCallCount);
    }

    private static UploadDocumentHandler CreateHandler(FakeDocumentRepository repository,
        FakeDocumentStorage storage, FakeExtractionClient extraction) =>
        new(repository, storage, extraction,
            new FakeExecutionContext(Guid.NewGuid(), Guid.NewGuid()), new FakeClock(Now));

    private static UploadDocumentCommand CreateCommand(string correlationId = "request-123") =>
        new("invoice.pdf", "application/pdf", 4, new MemoryStream([1, 2, 3, 4]), correlationId);

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public int? FailOnSaveCall { get; init; }
        public int SaveCallCount { get; private set; }
        public List<CancellationToken> SaveTokens { get; } = [];
        public List<bool> SaveTokenWasCancelledAtCall { get; } = [];
        public Document? AddedDocument { get; private set; }

        public Task AddAsync(Document document, CancellationToken cancellationToken)
        {
            AddedDocument = document;
            return Task.CompletedTask;
        }

        public Task<Document?> GetAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
            Task.FromResult(AddedDocument?.Id == documentId ? AddedDocument : null);

        public Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId,
            CancellationToken cancellationToken) => GetAsync(organizationId, documentId, cancellationToken);

        public Task<IReadOnlyList<Document>> ListAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Document>>(AddedDocument is null ? [] : [AddedDocument]);

        public Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId, CancellationToken cancellationToken) =>
            Task.FromResult((AddedDocument?.ExtractionRuns.Count ?? 0) + 1);

        public Task<DocumentMetrics> GetMetricsAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentMetrics(AddedDocument is null ? 0 : 1, 0, 0, null));

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCallCount++;
            SaveTokens.Add(cancellationToken);
            SaveTokenWasCancelledAtCall.Add(cancellationToken.IsCancellationRequested);
            if (SaveCallCount == FailOnSaveCall)
                throw new InvalidOperationException($"Persistence failure on save {SaveCallCount}.");
            return Task.CompletedTask;
        }

        public Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ReviewSaveOutcome.Saved);
    }

    private sealed class FakeDocumentStorage : IDocumentStorage
    {
        public StoredFile StoredFile { get; } = new(
            "stored/opaque-invoice.pdf",
            @"C:\safe-test-storage\opaque-invoice.pdf",
            4,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

        public int SaveCallCount { get; private set; }
        public List<string> DeletedStorageKeys { get; } = [];

        public Task<StoredFile> SaveAsync(Stream content, string originalFilename, long declaredSize,
            CancellationToken cancellationToken)
        {
            SaveCallCount++;
            return Task.FromResult(StoredFile);
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            DeletedStorageKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExtractionClient : IExtractionClient
    {
        public string ProviderName => "fake-provider";
        public string ProviderVersion => "1";
        public Action? BeforeCompletion { get; init; }
        public Exception? Exception { get; init; }
        public ExtractionResult Result { get; init; } = new("invoice", "en", "fake-extractor", []);

        public Task<ExtractionResult> ExtractAsync(string absolutePath, string contentType,
            CancellationToken cancellationToken)
        {
            BeforeCompletion?.Invoke();
            return Exception is null ? Task.FromResult(Result) : Task.FromException<ExtractionResult>(Exception);
        }
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed record FakeClock(DateTimeOffset UtcNow) : IClock;
}

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
            Result = CreateReadyResult("provider-returned-extractor-v7", pageNumber: 2)
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
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal(extraction.SchemaVersion, run.ExtractionSchemaVersion);
        Assert.Equal(extraction.ProviderName, run.ProviderName);
        Assert.Equal(extraction.ProviderVersion, run.ProviderVersion);
        Assert.Equal("provider-returned-extractor-v7", run.ModelName);
        Assert.Equal("v1", run.ModelVersion);
    }

    [Fact]
    public async Task Matching_nondefault_client_schema_is_accepted_and_persisted()
    {
        var repository = new FakeDocumentRepository();
        var extraction = new FakeExtractionClient
        {
            SchemaVersion = "2.1",
            Result = CreateReadyResult() with { SchemaVersion = "2.1" }
        };

        var result = await CreateHandler(repository, new FakeDocumentStorage(), extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Ready", result.Status);
        Assert.Equal("2.1", Assert.Single(repository.AddedDocument!.ExtractionRuns).ExtractionSchemaVersion);
    }

    [Theory]
    [InlineData("2.1", "2.0")]
    [InlineData("2.0", "1.0")]
    public async Task Result_schema_must_match_client_schema_version(string clientSchema, string resultSchema)
    {
        var repository = new FakeDocumentRepository();
        var extraction = new FakeExtractionClient
        {
            SchemaVersion = clientSchema,
            Result = CreateReadyResult() with { SchemaVersion = resultSchema }
        };

        var result = await CreateHandler(repository, new FakeDocumentStorage(), extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("extraction_failed", Assert.Single(repository.AddedDocument!.ExtractionRuns).FailureCode);
    }

    [Fact]
    public async Task Result_adapter_version_must_match_requested_provider_version()
    {
        var repository = new FakeDocumentRepository();
        var ready = CreateReadyResult();
        var extraction = new FakeExtractionClient
        {
            Result = ready with
            {
                Provider = ready.Provider with { AdapterVersion = "unexpected-adapter-version" }
            }
        };

        var result = await CreateHandler(repository, new FakeDocumentStorage(), extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("extraction_failed", Assert.Single(repository.AddedDocument!.ExtractionRuns).FailureCode);
    }

    [Theory]
    [InlineData("", "v1")]
    [InlineData("mock-model", "")]
    public async Task Result_requires_model_and_model_version(string model, string modelVersion)
    {
        var repository = new FakeDocumentRepository();
        var ready = CreateReadyResult();
        var extraction = new FakeExtractionClient
        {
            Result = ready with
            {
                Provider = ready.Provider with { Model = model, ModelVersion = modelVersion }
            }
        };

        var result = await CreateHandler(repository, new FakeDocumentStorage(), extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal("extraction_failed", run.FailureCode);
        Assert.Null(run.ModelName);
        Assert.Null(run.ModelVersion);
    }

    [Fact]
    public async Task Partial_result_is_reviewable_and_preserves_null_confidence_and_warning()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var ready = CreateReadyResult();
        var field = ready.Fields.Single() with { Confidence = null };
        var extraction = new FakeExtractionClient
        {
            Result = ready with
            {
                Outcome = ExtractionOutcome.Partial,
                Fields = [field],
                Warnings = [new ExtractionWarningResult("page_ocr_degraded",
                    ExtractionWarningSeverity.Degraded, "One page produced incomplete OCR text.",
                    1, null, null)]
            }
        };

        var result = await CreateHandler(repository, storage, extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Ready", result.Status);
        Assert.Null(Assert.Single(result.Fields).Confidence);
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal(ExtractionRunStatus.Partial, run.Status);
        Assert.Single(run.Warnings);
        Assert.Same(run, repository.AddedDocument.SelectedExtractionRun());
    }

    [Fact]
    public async Task Invalid_normalized_result_fails_safely_without_persisting_partial_output()
    {
        var repository = new FakeDocumentRepository();
        var storage = new FakeDocumentStorage();
        var ready = CreateReadyResult();
        var invalidEvidence = ready.Fields.Single().Evidence.Single() with { ByteEnd = 4 };
        var invalidField = ready.Fields.Single() with { Evidence = [invalidEvidence] };
        var extraction = new FakeExtractionClient { Result = ready with { Fields = [invalidField] } };

        var result = await CreateHandler(repository, storage, extraction)
            .HandleAsync(CreateCommand(), CancellationToken.None);

        Assert.Equal("Failed", result.Status);
        Assert.Equal("The extraction provider could not process this document.", result.ErrorMessage);
        var run = Assert.Single(repository.AddedDocument!.ExtractionRuns);
        Assert.Equal(ExtractionRunStatus.Failed, run.Status);
        Assert.Empty(run.Pages);
        Assert.Empty(run.Fields);
        Assert.Empty(run.Warnings);
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
        Assert.Equal(ExtractionRunStatus.Failed, run.Status);
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

    private static ExtractionResult CreateReadyResult(string model = "fake-extractor", int pageNumber = 1)
    {
        var pages = Enumerable.Range(1, pageNumber)
            .Select(number => new ExtractionPageResult(number, number == pageNumber ? "105.00" : string.Empty, 0))
            .ToList();
        return new ExtractionResult("2.0", ExtractionOutcome.Ready, "invoice", "en",
            new ExtractionProviderResult("fake-provider", "1", model, "v1"),
            new ExtractionMetricsResult(1, pages.Count, null, null, null), pages,
            [new ExtractionField("total", 0, FieldValueType.Decimal, "105.00", "105.00", 0.98m,
                [new ExtractionEvidenceResult(pageNumber, "105.00", 0, 6, null, null)])], []);
    }

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
        public string SchemaVersion { get; init; } = "2.0";
        public string ProviderName { get; init; } = "fake-provider";
        public string ProviderVersion { get; init; } = "1";
        public Action? BeforeCompletion { get; init; }
        public Exception? Exception { get; init; }
        public ExtractionResult Result { get; init; } = CreateReadyResult();

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

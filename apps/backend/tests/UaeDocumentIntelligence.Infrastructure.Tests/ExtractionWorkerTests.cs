using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class ExtractionWorkerTests
{
    [Fact]
    public async Task Worker_builds_exact_versioned_request_and_completes_current_claim()
    {
        var claim = CreateClaim();
        var queue = new FakeQueue();
        var client = new FakeClient { Result = CreateReadyResult() };
        using var provider = CreateProvider(queue, client);
        var worker = CreateWorker(provider);

        await ProcessAsync(worker, claim, CancellationToken.None);

        var request = Assert.IsType<WorkerExtractionRequest>(client.Request);
        Assert.Equal(claim.SchemaVersion, request.SchemaVersion);
        Assert.Equal(claim.JobId, request.JobId);
        Assert.Equal(claim.DocumentId, request.DocumentId);
        Assert.Equal(claim.ExtractionRunId, request.ExtractionRunId);
        Assert.Equal(claim.StorageKey, request.StorageKey);
        Assert.Equal(claim.Sha256, request.Sha256);
        Assert.Equal(claim.Bytes, request.Bytes);
        Assert.Equal(claim.DeclaredContentType, request.DeclaredContentType);
        Assert.Equal(claim.ProviderAlias, request.ProviderAlias);
        Assert.Equal(claim.Attempt, request.Attempt);
        Assert.Equal(claim.DeadlineUtc, request.DeadlineUtc);
        Assert.Equal(claim.CorrelationId, request.CorrelationId);
        Assert.True(client.Token.CanBeCanceled);
        Assert.Equal(claim, queue.CompletedClaim);
        Assert.Same(client.Result, queue.CompletedResult);
        Assert.Null(queue.FailedClaim);
    }

    [Theory]
    [InlineData("provider_network_error", true)]
    [InlineData("provider_timeout", true)]
    [InlineData("provider_throttled", true)]
    [InlineData("provider_unavailable", true)]
    [InlineData("unsupported_document", false)]
    [InlineData("provider_rejected", false)]
    [InlineData("malformed_provider_result", false)]
    [InlineData("request_cancelled", false)]
    public async Task Provider_classification_is_forwarded_without_worker_retry_amplification(
        string code, bool retryable)
    {
        var queue = new FakeQueue();
        var client = new FakeClient
        {
            Exception = new ExtractionProviderException(code, "Safe provider summary.", retryable)
        };
        using var provider = CreateProvider(queue, client);

        await ProcessAsync(CreateWorker(provider), CreateClaim(), CancellationToken.None);

        Assert.Equal(code, queue.FailureCode);
        Assert.Equal("Safe provider summary.", queue.FailureSummary);
        Assert.Equal(retryable, queue.FailureRetryable);
        Assert.Equal(1, queue.RecordFailureCallCount);
        Assert.Null(queue.CompletedClaim);
    }

    [Fact]
    public async Task Attempt_cancellation_without_shutdown_maps_to_retryable_provider_timeout()
    {
        var queue = new FakeQueue();
        var client = new FakeClient { Exception = new OperationCanceledException("timeout") };
        using var provider = CreateProvider(queue, client);

        await ProcessAsync(CreateWorker(provider), CreateClaim(), CancellationToken.None);

        Assert.Equal("provider_timeout", queue.FailureCode);
        Assert.True(queue.FailureRetryable);
        Assert.Equal("The extraction provider did not finish within this attempt.",
            queue.FailureSummary);
    }

    [Fact]
    public async Task Unexpected_provider_exception_is_mapped_to_safe_permanent_error()
    {
        const string sensitive = @"C:\private\invoice.pdf token-secret";
        var queue = new FakeQueue();
        var client = new FakeClient { Exception = new InvalidOperationException(sensitive) };
        using var provider = CreateProvider(queue, client);

        await ProcessAsync(CreateWorker(provider), CreateClaim(), CancellationToken.None);

        Assert.Equal("internal_extraction_error", queue.FailureCode);
        Assert.False(queue.FailureRetryable);
        Assert.DoesNotContain("C:\\private", queue.FailureSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token-secret", queue.FailureSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_shutdown_during_provider_call_leaves_lease_for_expiry_recovery()
    {
        using var stopping = new CancellationTokenSource();
        var queue = new FakeQueue();
        var client = new FakeClient
        {
            BeforeCompletion = stopping.Cancel,
            Exception = new OperationCanceledException(stopping.Token)
        };
        using var provider = CreateProvider(queue, client);

        await ProcessAsync(CreateWorker(provider), CreateClaim(), stopping.Token);

        Assert.Null(queue.CompletedClaim);
        Assert.Equal(0, queue.RecordFailureCallCount);
    }

    private static ServiceProvider CreateProvider(FakeQueue queue, FakeClient client)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IExtractionJobQueue>(queue);
        services.AddSingleton<IExtractionClient>(client);
        return services.BuildServiceProvider();
    }

    private static ExtractionWorker CreateWorker(ServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new ExtractionWorkerOptions { Enabled = true }),
        NullLogger<ExtractionWorker>.Instance);

    private static async Task ProcessAsync(ExtractionWorker worker, ClaimedExtractionJob claim,
        CancellationToken cancellationToken)
    {
        var method = typeof(ExtractionWorker).GetMethod("ProcessAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExtractionWorker.ProcessAsync was not found.");
        var task = method.Invoke(worker, [claim, cancellationToken]) as Task
            ?? throw new InvalidOperationException("ExtractionWorker.ProcessAsync did not return a task.");
        await task;
    }

    private static ClaimedExtractionJob CreateClaim() => new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        Guid.Parse("30000000-0000-0000-0000-000000000003"),
        Guid.Parse("40000000-0000-0000-0000-000000000004"),
        "organizations/tenant/documents/document/source.pdf",
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        12345, "application/pdf", "local", "local-1.0.0", "2.0",
        "request-123", 2,
        Guid.Parse("50000000-0000-0000-0000-000000000005"),
        "worker-a", DateTimeOffset.UtcNow.AddMinutes(2), DateTimeOffset.UtcNow.AddSeconds(-10));

    private static ExtractionResult CreateReadyResult() => new(
        "2.0", ExtractionOutcome.Ready, "invoice", "en",
        new ExtractionProviderResult("local", "local-1.0.0", "tesseract-eng", "5.3.0"),
        new ExtractionMetricsResult(1, 1, null, null, null),
        [new ExtractionPageResult(1, "105.00", 0)],
        [new ExtractionField("total", 0, FieldValueType.Decimal, "105.00", "105.00", 1,
            [new ExtractionEvidenceResult(1, "105.00", 0, 6, null, null)])], []);

    private sealed class FakeClient : IExtractionClient
    {
        public string ProviderName => "local";
        public string ProviderVersion => "local-1.0.0";
        public string SchemaVersion => "2.0";
        public WorkerExtractionRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public ExtractionResult Result { get; init; } = CreateReadyResult();
        public Exception? Exception { get; init; }
        public Action? BeforeCompletion { get; init; }

        public Task<ExtractionResult> ExtractAsync(WorkerExtractionRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Token = cancellationToken;
            BeforeCompletion?.Invoke();
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<ExtractionResult>(Exception);
        }
    }

    private sealed class FakeQueue : IExtractionJobQueue
    {
        public ClaimedExtractionJob? CompletedClaim { get; private set; }
        public ExtractionResult? CompletedResult { get; private set; }
        public ClaimedExtractionJob? FailedClaim { get; private set; }
        public string? FailureCode { get; private set; }
        public string? FailureSummary { get; private set; }
        public bool FailureRetryable { get; private set; }
        public int RecordFailureCallCount { get; private set; }

        public Task<bool> TerminalizeOneExpiredAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task<ClaimedExtractionJob?> ClaimAsync(string owner, CancellationToken cancellationToken) =>
            Task.FromResult<ClaimedExtractionJob?>(null);
        public Task<bool> HeartbeatAsync(Guid jobId, string owner, Guid leaseToken,
            CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<bool> CompleteAsync(ClaimedExtractionJob claim, ExtractionResult result,
            CancellationToken cancellationToken)
        {
            CompletedClaim = claim;
            CompletedResult = result;
            return Task.FromResult(true);
        }
        public Task<bool> RecordFailureAsync(ClaimedExtractionJob claim, string code,
            string safeSummary, bool retryable, CancellationToken cancellationToken)
        {
            FailedClaim = claim;
            FailureCode = code;
            FailureSummary = safeSummary;
            FailureRetryable = retryable;
            RecordFailureCallCount++;
            return Task.FromResult(true);
        }
        public Task<bool> CancelAsync(Guid organizationId, Guid jobId, string code,
            string safeSummary, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<ExtractionJobSnapshot?> GetLatestAsync(Guid organizationId, Guid documentId,
            CancellationToken cancellationToken) => Task.FromResult<ExtractionJobSnapshot?>(null);
        public Task<QueuedReprocess?> QueueReprocessAsync(Guid organizationId, Guid documentId,
            Guid runId, Guid jobId, string providerAlias, string providerVersion,
            string schemaVersion, string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<QueuedReprocess?>(null);
    }
}

using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Documents.Processing;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class ProcessingHandlerTests
{
    private static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid DocumentId = Guid.Parse("20000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Status_and_cancel_are_tenant_scoped_and_use_safe_cancellation_contract()
    {
        var queue = new FakeQueue();
        var execution = new FakeExecutionContext(OrganizationId, Guid.NewGuid());

        await new GetDocumentProcessingStatusHandler(queue, execution)
            .HandleAsync(DocumentId, CancellationToken.None);
        await new CancelExtractionHandler(queue, execution)
            .HandleAsync(Guid.Parse("30000000-0000-0000-0000-000000000003"),
                CancellationToken.None);

        Assert.Equal(OrganizationId, queue.LatestOrganizationId);
        Assert.Equal(DocumentId, queue.LatestDocumentId);
        Assert.Equal(OrganizationId, queue.CancelOrganizationId);
        Assert.Equal("request_cancelled", queue.CancelCode);
        Assert.Equal("Extraction was cancelled by an authorized workflow request.",
            queue.CancelSummary);
    }

    [Fact]
    public async Task Reprocess_uses_configured_provider_identity_and_new_nonempty_ids()
    {
        var queue = new FakeQueue();
        var handler = new ReprocessDocumentHandler(queue, new FakeExtractionClient(),
            new FakeExecutionContext(OrganizationId, Guid.NewGuid()));

        await handler.HandleAsync(DocumentId, "request-123", CancellationToken.None);

        Assert.Equal(OrganizationId, queue.ReprocessOrganizationId);
        Assert.Equal(DocumentId, queue.ReprocessDocumentId);
        Assert.NotEqual(Guid.Empty, queue.ReprocessRunId);
        Assert.NotEqual(Guid.Empty, queue.ReprocessJobId);
        Assert.NotEqual(queue.ReprocessRunId, queue.ReprocessJobId);
        Assert.Equal("local", queue.ProviderAlias);
        Assert.Equal("local-1.0.0", queue.ProviderVersion);
        Assert.Equal("2.0", queue.SchemaVersion);
        Assert.Equal("request-123", queue.CorrelationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains/slash")]
    public async Task Reprocess_rejects_unsafe_correlation_before_queue_call(string correlationId)
    {
        var queue = new FakeQueue();
        var handler = new ReprocessDocumentHandler(queue, new FakeExtractionClient(),
            new FakeExecutionContext(OrganizationId, Guid.NewGuid()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(DocumentId, correlationId, CancellationToken.None));

        Assert.Equal(0, queue.ReprocessCallCount);
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;

    private sealed class FakeExtractionClient : IExtractionClient
    {
        public string ProviderName => "local";
        public string ProviderVersion => "local-1.0.0";
        public string SchemaVersion => "2.0";
        public Task<ExtractionResult> ExtractAsync(WorkerExtractionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeQueue : IExtractionJobQueue
    {
        public Guid? LatestOrganizationId { get; private set; }
        public Guid? LatestDocumentId { get; private set; }
        public Guid? CancelOrganizationId { get; private set; }
        public string? CancelCode { get; private set; }
        public string? CancelSummary { get; private set; }
        public Guid? ReprocessOrganizationId { get; private set; }
        public Guid? ReprocessDocumentId { get; private set; }
        public Guid ReprocessRunId { get; private set; }
        public Guid ReprocessJobId { get; private set; }
        public string? ProviderAlias { get; private set; }
        public string? ProviderVersion { get; private set; }
        public string? SchemaVersion { get; private set; }
        public string? CorrelationId { get; private set; }
        public int ReprocessCallCount { get; private set; }

        public Task<ExtractionJobSnapshot?> GetLatestAsync(Guid organizationId, Guid documentId,
            CancellationToken cancellationToken)
        {
            LatestOrganizationId = organizationId;
            LatestDocumentId = documentId;
            return Task.FromResult<ExtractionJobSnapshot?>(null);
        }

        public Task<bool> CancelAsync(Guid organizationId, Guid jobId, string code,
            string safeSummary, CancellationToken cancellationToken)
        {
            CancelOrganizationId = organizationId;
            CancelCode = code;
            CancelSummary = safeSummary;
            return Task.FromResult(true);
        }

        public Task<QueuedReprocess?> QueueReprocessAsync(Guid organizationId, Guid documentId,
            Guid runId, Guid jobId, string providerAlias, string providerVersion,
            string schemaVersion, string correlationId, CancellationToken cancellationToken)
        {
            ReprocessCallCount++;
            ReprocessOrganizationId = organizationId;
            ReprocessDocumentId = documentId;
            ReprocessRunId = runId;
            ReprocessJobId = jobId;
            ProviderAlias = providerAlias;
            ProviderVersion = providerVersion;
            SchemaVersion = schemaVersion;
            CorrelationId = correlationId;
            return Task.FromResult<QueuedReprocess?>(new(jobId, runId, 2));
        }

        public Task<bool> TerminalizeOneExpiredAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ClaimedExtractionJob?> ClaimAsync(string owner, CancellationToken cancellationToken) =>
            Task.FromResult<ClaimedExtractionJob?>(null);
        public Task<bool> HeartbeatAsync(Guid jobId, string owner, Guid leaseToken,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> CompleteAsync(ClaimedExtractionJob claim, ExtractionResult result,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> RecordFailureAsync(ClaimedExtractionJob claim, string code,
            string safeSummary, bool retryable, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}

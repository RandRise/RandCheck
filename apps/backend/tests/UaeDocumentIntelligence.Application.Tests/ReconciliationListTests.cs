using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class ReconciliationListTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(9950, 10000)]
    [InlineData(10000, null)]
    public async Task List_uses_execution_tenant_bounded_lookahead_and_never_emits_invalid_offset(
        int offset, int? nextOffset)
    {
        var organization = Guid.NewGuid();
        var store = new RecordingStore();
        var handler = new ListReconciliationCasesHandler(store, new Execution(organization, Guid.NewGuid()));
        var result = await handler.HandleAsync(offset, default);
        Assert.Equal(organization, store.Organization);
        Assert.Equal(offset, store.Offset);
        Assert.Equal(51, store.Limit);
        Assert.Equal(50, result.Cases.Count);
        Assert.Equal(nextOffset, result.NextOffset);
    }

    private sealed record Execution(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed class RecordingStore : IReconciliationRepository
    {
        public Guid Organization { get; private set; }
        public Task<ReconciliationCase?> GetByCreationRequestAsync(Guid organizationId, Guid actorId,
            string key, CancellationToken token) => throw new NotSupportedException();
        public int Offset { get; private set; }
        public int Limit { get; private set; }
        public Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid organizationId,
            int offset, int limit, CancellationToken token, ReconciliationCaseFilter? filter = null)
        {
            Organization = organizationId; Offset = offset; Limit = limit;
            return Task.FromResult<IReadOnlyList<ReconciliationCaseSummary>>(Enumerable.Range(0, 51)
                .Select(i => new ReconciliationCaseSummary(Guid.NewGuid(), $"Case {i}", "Draft",
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 0, 0)).ToList());
        }
        public Task AddAsync(ReconciliationCase item, CancellationToken token) => throw new NotSupportedException();
        public Task<ReconciliationCase?> GetAsync(Guid organizationId, Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken token) => throw new NotSupportedException();
    }
}

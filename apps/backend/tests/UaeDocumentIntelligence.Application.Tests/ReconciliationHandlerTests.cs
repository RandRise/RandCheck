using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Application.Reconciliation;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class ReconciliationHandlerTests
{
    private const string ValidSha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 19, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Link_requires_completed_review_and_matching_document_role()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var (quotation, review) = CreateReviewedDocument(organizationId, actorId,
            "quotation", complete: false);
        var item = new ReconciliationCase(Guid.NewGuid(), organizationId, actorId, "Case", Now);
        var repository = new FakeReconciliationRepository(item);
        var documents = new FakeDocumentRepository(quotation);
        var handler = new LinkReconciliationDocumentHandler(repository, documents,
            new FakeExecutionContext(organizationId, actorId), new FakeClock(Now));

        var incomplete = await Assert.ThrowsAsync<ReconciliationValidationException>(() =>
            handler.HandleAsync(item.Id, item.ConcurrencyToken,
                new LinkReconciliationDocumentCommand("Quotation", quotation.Id, review.Id),
                CancellationToken.None));
        Assert.Equal("review_not_completed", incomplete.Code);

        Complete(quotation, review, Now.AddMinutes(1));
        var mismatch = await Assert.ThrowsAsync<ReconciliationValidationException>(() =>
            handler.HandleAsync(item.Id, item.ConcurrencyToken,
                new LinkReconciliationDocumentCommand("Invoice", quotation.Id, review.Id),
                CancellationToken.None));
        Assert.Equal("document_role_mismatch", mismatch.Code);

        var linked = await handler.HandleAsync(item.Id, item.ConcurrencyToken,
            new LinkReconciliationDocumentCommand("Quotation", quotation.Id, review.Id),
            CancellationToken.None);
        var source = Assert.Single(linked.Documents);
        Assert.Equal("Quotation", source.Role);
        Assert.Equal(review.ExtractionRunId, source.ExtractionRunId);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Evaluation_projects_pinned_effective_values_and_records_explainable_mismatch()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var quotation = CreateReviewedDocument(organizationId, actorId, "quotation").Document;
        var purchaseOrder = CreateReviewedDocument(organizationId, actorId, "purchase_order").Document;
        var invoiceResult = CreateReviewedDocument(organizationId, actorId, "invoice",
            correctedTotal: "11500.00");
        var invoice = invoiceResult.Document;
        var item = new ReconciliationCase(Guid.NewGuid(), organizationId, actorId, "Case", Now);
        Link(item, ReconciliationDocumentRole.Quotation, quotation);
        Link(item, ReconciliationDocumentRole.PurchaseOrder, purchaseOrder);
        Link(item, ReconciliationDocumentRole.Invoice, invoice);
        var repository = new FakeReconciliationRepository(item);
        var documents = new FakeDocumentRepository(quotation, purchaseOrder, invoice);
        var handler = new EvaluateReconciliationCaseHandler(repository, documents,
            new FakeExecutionContext(organizationId, actorId), new FakeClock(Now));

        var result = await handler.HandleAsync(item.Id, item.ConcurrencyToken,
            new EvaluateReconciliationCaseCommand(), CancellationToken.None);

        Assert.Equal("Evaluated", result.Status);
        var run = Assert.Single(result.Runs);
        var mismatch = Assert.Single(run.Findings.Where(finding => finding.RuleCode == "AMT-002"));
        Assert.Equal("10000.00", mismatch.LeftValue);
        Assert.Equal("11500.00", mismatch.RightValue);
        Assert.Equal(1500m, mismatch.Difference);
        Assert.Equal(0.01m, mismatch.Tolerance);
        var invoiceTotal = run.Fields.Single(field => field.Key == "total" &&
            field.Value == "11500.00");
        Assert.NotNull(invoiceTotal.MachineFieldValueId);
        Assert.NotNull(invoiceTotal.ReviewActionId);
        Assert.Equal(1, invoiceTotal.Page);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Evaluation_rejects_stale_case_version_without_creating_run()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var invoice = CreateReviewedDocument(organizationId, actorId, "invoice").Document;
        var item = new ReconciliationCase(Guid.NewGuid(), organizationId, actorId, "Case", Now);
        Link(item, ReconciliationDocumentRole.Invoice, invoice);
        var repository = new FakeReconciliationRepository(item);
        var handler = new EvaluateReconciliationCaseHandler(repository,
            new FakeDocumentRepository(invoice), new FakeExecutionContext(organizationId, actorId),
            new FakeClock(Now));

        var error = await Assert.ThrowsAsync<ReconciliationPreconditionException>(() =>
            handler.HandleAsync(item.Id, Guid.NewGuid(), new EvaluateReconciliationCaseCommand(),
                CancellationToken.None));

        Assert.Equal("stale_reconciliation_version", error.Code);
        Assert.Empty(item.Runs);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Resolution_is_attributed_append_only_and_rotates_case_version()
    {
        var organizationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var invoice = CreateReviewedDocument(organizationId, actorId, "invoice").Document;
        var item = new ReconciliationCase(Guid.NewGuid(), organizationId, actorId, "Case", Now);
        Link(item, ReconciliationDocumentRole.Invoice, invoice);
        var repository = new FakeReconciliationRepository(item);
        var documents = new FakeDocumentRepository(invoice);
        var evaluation = await new EvaluateReconciliationCaseHandler(repository, documents,
            new FakeExecutionContext(organizationId, actorId), new FakeClock(Now))
            .HandleAsync(item.Id, item.ConcurrencyToken,
                new EvaluateReconciliationCaseCommand(), CancellationToken.None);
        var run = Assert.Single(evaluation.Runs);
        var finding = run.Findings.First(candidate => candidate.RuleCode == "DOC-001");
        var before = item.ConcurrencyToken;

        var resolved = await new ResolveReconciliationFindingHandler(repository,
            new FakeExecutionContext(organizationId, actorId), new FakeClock(Now.AddMinutes(1)))
            .HandleAsync(item.Id, run.Id, finding.Id, before,
                new ResolveFindingCommand("Accepted", "Invoice-only development exception"),
                CancellationToken.None);

        Assert.NotEqual(before, item.ConcurrencyToken);
        var resolution = Assert.Single(Assert.Single(resolved.Runs).Findings
            .Single(candidate => candidate.Id == finding.Id).Resolutions);
        Assert.Equal("Accepted", resolution.Action);
        Assert.Equal(actorId, resolution.ActorId);
        Assert.Equal("Invoice-only development exception", resolution.Reason);
    }

    [Fact]
    public async Task Create_case_is_organization_scoped_and_attributed()
    {
        var execution = new FakeExecutionContext(Guid.NewGuid(), Guid.NewGuid());
        var repository = new FakeReconciliationRepository();
        var result = await new CreateReconciliationCaseHandler(repository, execution,
            new FakeClock(Now)).HandleAsync("August case", CancellationToken.None);

        Assert.Equal("August case", result.Name);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(execution.OrganizationId, repository.Item?.OrganizationId);
        Assert.Equal(execution.ActorId, repository.Item?.CreatedByActorId);
    }

    private static (Document Document, ReviewSession Review) CreateReviewedDocument(
        Guid organizationId, Guid actorId, string documentType, bool complete = true,
        string? correctedTotal = null)
    {
        var document = new Document(Guid.NewGuid(), organizationId, actorId,
            $"{documentType}.pdf", $"{Guid.NewGuid():N}.pdf", "application/pdf", 100,
            ValidSha, Now);
        var run = document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "2.0",
            Guid.NewGuid().ToString("N"), Now);
        run.Start(Now);
        run.AddPage(Guid.NewGuid(), 1, "Reviewed procurement values", 0);
        var values = new (string Key, FieldValueType Type, string Value, int Occurrence)[]
        {
            ("supplier_trn", FieldValueType.Identifier, "100000000000003", 0),
            ("currency", FieldValueType.Currency, "AED", 0),
            ("total", FieldValueType.Decimal, "10000.00", 0),
            ("vat", FieldValueType.Decimal, "500.00", 0),
            ("line_item_code", FieldValueType.Identifier, "CONSULT", 1),
            ("line_description", FieldValueType.String, "Consulting service", 1),
            ("line_quantity", FieldValueType.Decimal, "1", 1),
            ("line_unit_price", FieldValueType.Decimal, "9500.00", 1),
            ("line_tax", FieldValueType.Decimal, "500.00", 1),
            ("line_total", FieldValueType.Decimal, "10000.00", 1)
        };
        foreach (var value in values)
        {
            var field = run.AddField(Guid.NewGuid(), value.Key, value.Occurrence, value.Type,
                value.Value, 0.99m, Now, value.Value);
            run.AddEvidence(field, Guid.NewGuid(), 1);
        }
        run.RecordProviderMetrics(1, 1, null, null, null);
        run.Succeed(documentType, "en", Now, "mock", "1");
        document.RecordExtractionSuccess(run, Now);
        var review = document.OpenReview(Guid.NewGuid(), actorId, Now);
        var sequence = 1;
        foreach (var field in run.Fields.OrderBy(field => field.FieldKey)
            .ThenBy(field => field.OccurrenceIndex))
        {
            var correct = field.FieldKey == "total" && correctedTotal is not null;
            review.AddAction(Guid.NewGuid(), actorId, sequence++, correct
                    ? ReviewActionType.Corrected : ReviewActionType.Confirmed,
                field.FieldKey, field.OccurrenceIndex, field.Id,
                correct ? FieldValueType.Decimal : null, correct ? correctedTotal : null,
                correct ? "Reviewed invoice total" : null, Now);
        }
        if (complete) review.Complete(run.Fields, Now);
        return (document, review);
    }

    private static void Complete(Document document, ReviewSession review, DateTimeOffset now)
    {
        var run = document.ExtractionRuns.Single(item => item.Id == review.ExtractionRunId);
        review.Complete(run.Fields, now);
    }

    private static void Link(ReconciliationCase item, ReconciliationDocumentRole role,
        Document document)
    {
        var review = document.ReviewSessions.Single(item => item.Status == ReviewStatus.Completed);
        item.LinkReviewedDocument(Guid.NewGuid(), role, document.Id, review.Id,
            review.ExtractionRunId, document.Sha256, Now);
    }

    private sealed class FakeReconciliationRepository(ReconciliationCase? item = null)
        : IReconciliationRepository
    {
        public Task<IReadOnlyList<ReconciliationCaseSummary>> ListAsync(Guid organizationId,
            int offset, int limit, CancellationToken token, ReconciliationCaseFilter? filter = null) =>
            Task.FromResult<IReadOnlyList<ReconciliationCaseSummary>>([]);
        public ReconciliationCase? Item { get; private set; } = item;
        public Task<ReconciliationCase?> GetByCreationRequestAsync(Guid organizationId, Guid actorId,
            string key, CancellationToken token) => Task.FromResult(Item is not null && Item.OrganizationId == organizationId &&
                Item.CreatedByActorId == actorId && Item.CreationRequestKey == key ? Item : null);
        public int SaveCount { get; private set; }
        public ReconciliationSaveOutcome Outcome { get; init; } = ReconciliationSaveOutcome.Saved;
        public Task AddAsync(ReconciliationCase value, CancellationToken token)
        {
            Item = value;
            return Task.CompletedTask;
        }
        public Task<ReconciliationCase?> GetAsync(Guid organizationId, Guid id,
            CancellationToken token) => Task.FromResult(Item is not null &&
            Item.OrganizationId == organizationId && Item.Id == id ? Item : null);
        public Task<ReconciliationSaveOutcome> SaveChangesAsync(CancellationToken token)
        {
            SaveCount++;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakeDocumentRepository(params Document[] documents) : IDocumentRepository
    {
        public Task<Document?> GetAsync(Guid organizationId, Guid documentId,
            CancellationToken token) => Task.FromResult(documents.SingleOrDefault(item =>
                item.OrganizationId == organizationId && item.Id == documentId));
        public Task<Document?> GetForReviewAsync(Guid organizationId, Guid documentId,
            CancellationToken token) => GetAsync(organizationId, documentId, token);
        public Task AddAsync(Document document, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<Document>> ListAsync(Guid organizationId,
            CancellationToken token) => Task.FromResult<IReadOnlyList<Document>>([]);
        public Task<int> NextRunNumberAsync(Guid organizationId, Guid documentId,
            CancellationToken token) => Task.FromResult(1);
        public Task<DocumentMetrics> GetMetricsAsync(Guid organizationId,
            CancellationToken token) => Task.FromResult(new DocumentMetrics(0, 0, 0, null));
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
        public Task<ReviewSaveOutcome> SaveReviewChangesAsync(CancellationToken token) =>
            Task.FromResult(ReviewSaveOutcome.Saved);
    }

    private sealed record FakeExecutionContext(Guid OrganizationId, Guid ActorId) : IExecutionContext;
    private sealed record FakeClock(DateTimeOffset UtcNow) : IClock;
}

using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public enum ReconciliationCaseCategory { Uncategorized, OfficeSupplies, Services, Equipment }
public enum ReconciliationWorkflowKind { Legacy, InvoicePurchaseOrder }

public sealed class ReconciliationCase
{
    private readonly List<ReconciliationDocumentLink> _documentLinks = [];
    private readonly List<ReconciliationRun> _runs = [];
    private ReconciliationCase() { }

    public ReconciliationCase(Guid id, Guid organizationId, Guid createdByActorId,
        string name, DateTimeOffset now, ReconciliationCaseCategory category = ReconciliationCaseCategory.Uncategorized,
        string? creationRequestKey = null, string? creationPayloadHash = null,
        ReconciliationWorkflowKind workflowKind = ReconciliationWorkflowKind.Legacy)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || createdByActorId == Guid.Empty)
            throw new ArgumentException("Case, organization, and actor IDs are required.");
        Id = id;
        OrganizationId = organizationId;
        CreatedByActorId = createdByActorId;
        Name = Guard.Required(name, nameof(name), 200);
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        Category = category;
        if (!Enum.IsDefined(workflowKind)) throw new ArgumentOutOfRangeException(nameof(workflowKind));
        WorkflowKind = workflowKind;
        if ((creationRequestKey is null) != (creationPayloadHash is null))
            throw new ArgumentException("Creation request identity must include its payload hash.");
        CreationRequestKey = creationRequestKey is null ? null : Guard.Required(creationRequestKey, nameof(creationRequestKey), 100);
        CreationPayloadHash = creationPayloadHash is null ? null : Guard.Required(creationPayloadHash, nameof(creationPayloadHash), 64);
        Status = ReconciliationCaseStatus.Draft;
        ConcurrencyToken = Guid.NewGuid();
        CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid CreatedByActorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public ReconciliationCaseCategory Category { get; private set; }
    public ReconciliationWorkflowKind WorkflowKind { get; private set; }
    public Guid? DraftInvoiceDocumentId { get; private set; }
    public Guid? DraftPurchaseOrderDocumentId { get; private set; }
    public Guid? DraftInvoiceVersion { get; private set; }
    public Guid? DraftPurchaseOrderVersion { get; private set; }
    public string? CreationRequestKey { get; private set; }
    public string? CreationPayloadHash { get; private set; }
    public ReconciliationCaseStatus Status { get; private set; }
    public Guid ConcurrencyToken { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public IReadOnlyCollection<ReconciliationDocumentLink> DocumentLinks => _documentLinks.AsReadOnly();
    public IReadOnlyCollection<ReconciliationRun> Runs => _runs.AsReadOnly();

    public Guid? GetDraftDocumentId(ReconciliationDocumentRole role) => role switch
    {
        ReconciliationDocumentRole.Invoice => DraftInvoiceDocumentId,
        ReconciliationDocumentRole.PurchaseOrder => DraftPurchaseOrderDocumentId,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public Guid? GetDraftVersion(ReconciliationDocumentRole role) => role switch
    {
        ReconciliationDocumentRole.Invoice => DraftInvoiceVersion,
        ReconciliationDocumentRole.PurchaseOrder => DraftPurchaseOrderVersion,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public void SetDraftDocument(ReconciliationDocumentRole role, Guid? documentId, DateTimeOffset now)
    {
        RequireMutable();
        if (WorkflowKind != ReconciliationWorkflowKind.InvoicePurchaseOrder)
            throw new InvalidOperationException("Draft documents require the invoice and PO workflow.");
        var current = GetDraftDocumentId(role);
        if (documentId == Guid.Empty) throw new ArgumentException("Document ID cannot be empty.");
        var other = role == ReconciliationDocumentRole.Invoice ? DraftPurchaseOrderDocumentId : DraftInvoiceDocumentId;
        if (documentId is not null && documentId == other)
            throw new InvalidOperationException("Invoice and PO must be different documents.");
        if (current == documentId) return;
        if (role == ReconciliationDocumentRole.Invoice)
        { DraftInvoiceDocumentId = documentId; DraftInvoiceVersion = Guid.NewGuid(); }
        else
        { DraftPurchaseOrderDocumentId = documentId; DraftPurchaseOrderVersion = Guid.NewGuid(); }
        _documentLinks.RemoveAll(link => link.Role == role);
        Touch(now);
    }

    public void RecordDraftIntent(DateTimeOffset now)
    {
        RequireMutable();
        if (WorkflowKind != ReconciliationWorkflowKind.InvoicePurchaseOrder)
            throw new InvalidOperationException("Draft intents require the invoice and PO workflow.");
        // Even a new no-op intent participates in the case's optimistic concurrency check.
        Touch(now);
    }

    public void UpdateMetadata(string name, ReconciliationCaseCategory category, DateTimeOffset now)
    {
        RequireMutable();
        var title = Guard.Required(name, nameof(name), 200);
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        if (Name == title && Category == category) return;
        Name = title;
        Category = category;
        Touch(now);
    }

    public ReconciliationDocumentLink LinkReviewedDocument(Guid linkId,
        ReconciliationDocumentRole role, Guid documentId, Guid reviewSessionId,
        Guid extractionRunId, string sha256, DateTimeOffset now)
    {
        RequireMutable();
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (WorkflowKind == ReconciliationWorkflowKind.InvoicePurchaseOrder &&
            (role is not (ReconciliationDocumentRole.Invoice or ReconciliationDocumentRole.PurchaseOrder) ||
             GetDraftDocumentId(role) != documentId))
            throw new InvalidOperationException("Choose the draft document before pinning its completed review.");
        if (linkId == Guid.Empty || documentId == Guid.Empty || reviewSessionId == Guid.Empty ||
            extractionRunId == Guid.Empty)
            throw new ArgumentException("Link, document, review, and extraction IDs are required.");
        if (_documentLinks.Any(link => link.Role == role))
            throw new InvalidOperationException("The document role is already linked.");

        var link = new ReconciliationDocumentLink(linkId, OrganizationId, Id, role,
            documentId, reviewSessionId, extractionRunId, sha256, now);
        _documentLinks.Add(link);
        _documentLinks.Sort((left, right) => left.Role.CompareTo(right.Role));
        Touch(now);
        return link;
    }

    public ReconciliationDocumentLink ReplaceReviewedDocument(Guid linkId,
        ReconciliationDocumentRole role, Guid documentId, Guid reviewSessionId,
        Guid extractionRunId, string sha256, DateTimeOffset now)
    {
        RequireMutable();
        var existing = _documentLinks.SingleOrDefault(link => link.Role == role)
            ?? throw new InvalidOperationException("The document role is not linked.");
        _documentLinks.Remove(existing);
        try
        {
            return LinkReviewedDocument(linkId, role, documentId, reviewSessionId,
                extractionRunId, sha256, now);
        }
        catch
        {
            _documentLinks.Add(existing);
            _documentLinks.Sort((left, right) => left.Role.CompareTo(right.Role));
            throw;
        }
    }

    public ReconciliationRun BeginEvaluation(Guid runId,
        IReadOnlyDictionary<ReconciliationDocumentRole, Guid> documentSnapshotIds,
        string ruleSetVersion, ReconciliationTolerancePolicy tolerancePolicy,
        DateTimeOffset now)
    {
        RequireMutable();
        if (WorkflowKind == ReconciliationWorkflowKind.InvoicePurchaseOrder &&
            (!InvoicePurchaseOrderRuleEngine.IsPairPolicy(ruleSetVersion) || _documentLinks.Count != 2 ||
             new[] { ReconciliationDocumentRole.Invoice, ReconciliationDocumentRole.PurchaseOrder }.Any(role =>
                 GetDraftDocumentId(role) is not { } documentId || !_documentLinks.Any(link => link.Role == role && link.DocumentId == documentId))))
            throw new InvalidOperationException("Invoice pairs require their pair policy and both exact draft review pins.");
        if (_documentLinks.Count == 0)
            throw new InvalidOperationException("At least one reviewed document is required.");
        if (_runs.Any(run => run.Status == ReconciliationRunStatus.Running))
            throw new InvalidOperationException("An evaluation is already running.");
        ArgumentNullException.ThrowIfNull(documentSnapshotIds);
        if (documentSnapshotIds.Count != _documentLinks.Count ||
            _documentLinks.Any(link => !documentSnapshotIds.TryGetValue(link.Role, out var id) || id == Guid.Empty))
            throw new ArgumentException("Every linked role requires one non-empty snapshot ID.",
                nameof(documentSnapshotIds));

        var runNumber = _runs.Select(run => run.RunNumber).DefaultIfEmpty(0).Max() + 1;
        var run = new ReconciliationRun(runId, OrganizationId, Id, runNumber,
            ruleSetVersion, tolerancePolicy, now);
        foreach (var link in _documentLinks)
            run.AddDocumentSnapshot(documentSnapshotIds[link.Role], link, now);
        _runs.Add(run);
        Touch(now);
        return run;
    }

    public void CompleteEvaluation(ReconciliationRun run, DateTimeOffset now)
    {
        if (!_runs.Contains(run))
            throw new InvalidOperationException("Evaluation run does not belong to this case.");
        run.Complete(now);
        Status = ReconciliationCaseStatus.Evaluated;
        Touch(now);
    }

    public FindingResolution ResolveFinding(Guid runId, Guid findingId, Guid resolutionId,
        Guid actorId, FindingResolutionAction action, string reason, DateTimeOffset now)
    {
        RequireMutable();
        var run = _runs.SingleOrDefault(item => item.Id == runId)
            ?? throw new InvalidOperationException("Evaluation run does not belong to this case.");
        var resolution = run.AddResolution(findingId, resolutionId, actorId, action, reason, now);
        Touch(now);
        return resolution;
    }

    public void Close(DateTimeOffset now)
    {
        RequireMutable();
        if (_runs.Any(run => run.Status == ReconciliationRunStatus.Running))
            throw new InvalidOperationException("A running evaluation must complete before closure.");
        Status = ReconciliationCaseStatus.Closed;
        ClosedAtUtc = now;
        Touch(now);
    }

    private void RequireMutable()
    {
        if (Status == ReconciliationCaseStatus.Closed)
            throw new InvalidOperationException("A closed reconciliation case cannot change.");
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class ReconciliationDocumentLink
{
    private ReconciliationDocumentLink() { }

    internal ReconciliationDocumentLink(Guid id, Guid organizationId, Guid reconciliationCaseId,
        ReconciliationDocumentRole role, Guid documentId, Guid reviewSessionId,
        Guid extractionRunId, string sha256, DateTimeOffset linkedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ReconciliationCaseId = reconciliationCaseId;
        Role = role;
        DocumentId = documentId;
        ReviewSessionId = reviewSessionId;
        ExtractionRunId = extractionRunId;
        Sha256 = ValidateSha256(sha256);
        LinkedAtUtc = linkedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReconciliationCaseId { get; private set; }
    public ReconciliationDocumentRole Role { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid ReviewSessionId { get; private set; }
    public Guid ExtractionRunId { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public DateTimeOffset LinkedAtUtc { get; private set; }

    private static string ValidateSha256(string value)
    {
        if (value is null || value.Length != 64 ||
            value.Any(character => !((character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'))))
            throw new ArgumentException("SHA-256 must be 64 lowercase hexadecimal characters.", nameof(value));
        return value;
    }
}

public sealed record ReconciliationTolerancePolicy(decimal Quantity, decimal UnitPrice,
    decimal Tax, decimal LineTotal, decimal DocumentTotal)
{
    public static ReconciliationTolerancePolicy Default { get; } = new(0m, 0.01m, 0.01m, 0.01m, 0.01m);

    internal void Validate()
    {
        if (Quantity < 0 || UnitPrice < 0 || Tax < 0 || LineTotal < 0 || DocumentTotal < 0)
            throw new ArgumentOutOfRangeException(nameof(ReconciliationTolerancePolicy),
                "Reconciliation tolerances cannot be negative.");
    }
}

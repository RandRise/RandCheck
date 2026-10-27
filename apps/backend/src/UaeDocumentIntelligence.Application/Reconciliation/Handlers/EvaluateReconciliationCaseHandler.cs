using System.Text.Json;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed class EvaluateReconciliationCaseHandler(IReconciliationRepository repository,
    IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, Guid concurrencyToken,
        EvaluateReconciliationCaseCommand command, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        if (concurrencyToken == Guid.Empty || item.ConcurrencyToken != concurrencyToken)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        var policy = Policy(command);
        var version = command.RuleSetVersion ?? ReconciliationRuleEngine.RuleSetVersion;
        if (version != ReconciliationRuleEngine.RuleSetVersion && !InvoicePurchaseOrderRuleEngine.IsPairPolicy(version))
            throw new ReconciliationValidationException("unsupported_rule_set", "Select 1.0, invoice-po-1.0 or invoice-po-2.0.");
        var pair = InvoicePurchaseOrderRuleEngine.IsPairPolicy(version);
        var factual = version == InvoicePurchaseOrderRuleEngine.FactualRuleSetVersion;
        if (factual && command.OrderScope is not null)
            throw new ReconciliationValidationException("scope_not_used", "The factual policy does not accept an order-scope declaration.");
        if (item.WorkflowKind == ReconciliationWorkflowKind.InvoicePurchaseOrder)
        {
            if (!pair || command.RuleSetVersion is null)
                throw new ReconciliationValidationException("pair_policy_required", "Saved invoice pairs require an explicit invoice/PO comparison policy.");
            if (!factual && command.OrderScope is null)
                throw new ReconciliationValidationException("pair_scope_required", "Choose Unknown, FullOrder or Partial explicitly before comparing.");
            if (new[] { ReconciliationDocumentRole.Invoice, ReconciliationDocumentRole.PurchaseOrder }.Any(role =>
                item.GetDraftDocumentId(role) is not { } documentId || !item.DocumentLinks.Any(link => link.Role == role && link.DocumentId == documentId)) ||
                item.DocumentLinks.Count != 2)
                throw new ReconciliationConflictException("pair_review_pins_required", "Choose exact completed reviews for both saved draft documents before comparing.");
        }
        var scope = InvoiceOrderScope.Unknown;
        if (pair)
        {
            if (command.OrderScope is not null && command.OrderScope is not ("Unknown" or "FullOrder" or "Partial"))
                throw new ReconciliationValidationException("invalid_order_scope", "Pair scope must be Unknown, FullOrder or Partial.");
            scope = command.OrderScope switch
            {
                "FullOrder" => InvoiceOrderScope.FullOrder,
                "Partial" => InvoiceOrderScope.Partial,
                _ => InvoiceOrderScope.Unknown
            };
            InvoicePurchaseOrderRuleEngine.ValidateTolerances(policy);
        }
        else if (command.OrderScope is not null)
            throw new ReconciliationValidationException("scope_requires_pair_policy", "Order scope applies only to invoice-po-1.0.");
        if (item.Status == ReconciliationCaseStatus.Closed)
            throw new ReconciliationConflictException("case_closed", "A closed reconciliation case cannot change.");
        if (item.DocumentLinks.Count == 0)
            throw new ReconciliationValidationException("case_has_no_documents", "Link a reviewed document before comparing.");
        // Validate the exact pinned sources before BeginEvaluation changes the case.
        // Reuse these same loaded reviews when taking the immutable snapshots.
        var sources = new List<(ReconciliationDocumentRole Role, Document Document, ReviewSession Review)>();
        foreach (var link in item.DocumentLinks)
        {
            var document = await documents.GetForReviewAsync(execution.OrganizationId,
                link.DocumentId, cancellationToken) ?? throw new ReconciliationNotFoundException();
            var session = LinkReconciliationDocumentHandler.RequireCompletedReview(document,
                link.ReviewSessionId);
            if (session.ExtractionRunId != link.ExtractionRunId || document.Sha256 != link.Sha256)
                throw new ReconciliationConflictException("reconciliation_source_changed",
                    "A linked reconciliation source no longer matches its pinned review.");
            if (item.WorkflowKind == ReconciliationWorkflowKind.InvoicePurchaseOrder)
                InvoicePairCurrencyValidator.RequireAed(link.Role, document, session);
            sources.Add((link.Role, document, session));
        }

        var snapshotIds = item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid());
        var run = item.BeginEvaluation(Guid.NewGuid(), snapshotIds,
            version, policy, clock.UtcNow);
        if (pair)
            run.SetPairContext(execution.ActorId, scope);

        foreach (var source in sources)
        {
            if (pair)
                ProjectPairValues(run, source.Role, source.Document, source.Review, clock.UtcNow);
            else
                ProjectReviewedValues(run, source.Role, source.Document, source.Review, clock.UtcNow);
        }

        if (pair)
            new InvoicePurchaseOrderRuleEngine().Evaluate(run, Guid.NewGuid, clock.UtcNow);
        else
            new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, clock.UtcNow);
        item.CompleteEvaluation(run, clock.UtcNow);
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == ReconciliationSaveOutcome.ConcurrencyConflict)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (outcome == ReconciliationSaveOutcome.ConstraintConflict)
        {
            // The run-number uniqueness check can win the race before the case ETag UPDATE.
            var current = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken);
            if (current is not null && current.ConcurrencyToken != concurrencyToken)
                throw new ReconciliationPreconditionException("stale_reconciliation_version",
                    "The reconciliation case changed. Reload it before retrying.");
            throw new ReconciliationConflictException("reconciliation_evaluation_conflict",
                "The reconciliation evaluation could not be saved atomically.");
        }
        return ReconciliationView.From(item);
    }

    internal static void ProjectReviewedValues(ReconciliationRun target,
        ReconciliationDocumentRole role, Document document, ReviewSession session,
        DateTimeOffset now)
    {
        var sourceRun = document.ExtractionRuns.Single(run => run.Id == session.ExtractionRunId);
        var snapshot = target.DocumentSnapshots.Single(item => item.Role == role);
        foreach (var machine in sourceRun.Fields.OrderBy(field => field.FieldKey)
            .ThenBy(field => field.OccurrenceIndex))
        {
            var action = session.Actions.Where(item => item.MachineFieldValueId == machine.Id)
                .OrderByDescending(item => item.SequenceNumber).FirstOrDefault();
            if (action?.Action == ReviewActionType.Cleared)
                continue;
            var value = action?.Action == ReviewActionType.Corrected
                ? action.CorrectedValueText!
                : machine.NormalizedValueText ?? machine.ValueText;
            var type = action?.Action == ReviewActionType.Corrected
                ? action.CorrectedValueType!.Value
                : machine.ValueType;
            var evidence = machine.Evidence.OrderBy(item => item.PageNumber).FirstOrDefault();
            target.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id, CanonicalKey(machine.FieldKey),
                machine.OccurrenceIndex, type, value, machine.Id, action?.Id,
                evidence?.PageNumber, evidence?.QuotedText, now);
        }

        var added = session.Actions.Where(action => !action.MachineFieldValueId.HasValue)
            .GroupBy(action => new { action.FieldKey, action.OccurrenceIndex })
            .Select(group => group.OrderByDescending(action => action.SequenceNumber).First())
            .Where(action => action.Action == ReviewActionType.Added)
            .OrderBy(action => action.FieldKey).ThenBy(action => action.OccurrenceIndex);
        foreach (var action in added)
            target.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id, CanonicalKey(action.FieldKey),
                action.OccurrenceIndex, action.CorrectedValueType!.Value,
                action.CorrectedValueText!, null, action.Id, null, null, now);
    }

    internal static void ProjectPairValues(ReconciliationRun target, ReconciliationDocumentRole role,
        Document document, ReviewSession session, DateTimeOffset now)
    {
        var sourceRun = document.ExtractionRuns.Single(run => run.Id == session.ExtractionRunId);
        var snapshot = target.DocumentSnapshots.Single(item => item.Role == role);
        var rows = sourceRun.Fields.Where(field => field.FieldKey.StartsWith("line_", StringComparison.Ordinal) &&
            field.OccurrenceIndex > 0).GroupBy(field => field.OccurrenceIndex).ToArray();
        // Only this explicitly supported adapter's successful, warning-free, non-empty row extraction can
        // establish candidate coverage. The engine additionally checks projected row count and checked fields.
        var completeRows = sourceRun.ProviderName == "local" && sourceRun.ProviderVersion == "local-1.6.0" && sourceRun.Status == ExtractionRunStatus.Succeeded &&
            sourceRun.Warnings.Count == 0 && rows.Length > 0 && rows.All(row =>
                new[] { "line_description", "line_quantity", "line_unit_price" }.All(key => row.Any(field =>
                    field.FieldKey == key && field.Evidence.Any(evidence => !string.IsNullOrWhiteSpace(evidence.QuotedText)))));
        target.CaptureSourceState(snapshot.Id, sourceRun.Status.ToString(), sourceRun.ProviderName,
            sourceRun.ProviderVersion, sourceRun.ModelName, sourceRun.ModelVersion,
            JsonSerializer.Serialize(sourceRun.Warnings.OrderBy(warning => warning.Code).ThenBy(warning => warning.PageNumber)
                .ThenBy(warning => warning.FieldKey).ThenBy(warning => warning.OccurrenceIndex).Select(warning => new
                {
                    warning.Code,
                    Severity = warning.Severity.ToString(),
                    warning.PageNumber,
                    warning.FieldKey,
                    warning.OccurrenceIndex,
                    warning.SafeSummary
                })), rows.Length, completeRows);
        foreach (var machine in sourceRun.Fields.OrderBy(field => field.FieldKey).ThenBy(field => field.OccurrenceIndex))
        {
            var action = session.Actions.Where(item => item.MachineFieldValueId == machine.Id)
                .OrderByDescending(item => item.SequenceNumber).FirstOrDefault();
            var corrected = action?.Action == ReviewActionType.Corrected;
            var evidence = machine.Evidence.OrderBy(item => item.PageNumber).FirstOrDefault();
            target.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id, machine.FieldKey.Trim().ToLowerInvariant(),
                machine.OccurrenceIndex, corrected ? action!.CorrectedValueType!.Value : machine.ValueType,
                corrected ? action!.CorrectedValueText! : machine.ValueText, machine.Id, action?.Id,
                evidence?.PageNumber, evidence?.QuotedText, now,
                action?.Action == ReviewActionType.Cleared ? ReviewedFieldSourceKind.Cleared
                    : corrected ? ReviewedFieldSourceKind.Corrected : action?.Action == ReviewActionType.Confirmed
                    ? ReviewedFieldSourceKind.Confirmed : ReviewedFieldSourceKind.Unchecked);
        }
        foreach (var action in session.Actions.Where(action => !action.MachineFieldValueId.HasValue)
            .GroupBy(action => new { action.FieldKey, action.OccurrenceIndex })
            .Select(group => group.OrderByDescending(action => action.SequenceNumber).First())
            .Where(action => action.Action == ReviewActionType.Added)
            .OrderBy(action => action.FieldKey).ThenBy(action => action.OccurrenceIndex))
            target.AddFieldSnapshot(Guid.NewGuid(), snapshot.Id, action.FieldKey.Trim().ToLowerInvariant(),
                action.OccurrenceIndex, action.CorrectedValueType!.Value, action.CorrectedValueText!,
                null, action.Id, null, null, now, ReviewedFieldSourceKind.Added);
    }

    private static string CanonicalKey(string key) => key.Trim().ToLowerInvariant() switch
    {
        "invoice_number" or "quotation_number" or "purchase_order_number" or "po_number" =>
            "document_number",
        "tax" or "vat_charged" => "vat",
        "gross_payable" => "total",
        _ => key.Trim().ToLowerInvariant()
    };

    private static ReconciliationTolerancePolicy Policy(EvaluateReconciliationCaseCommand command)
    {
        var defaults = ReconciliationTolerancePolicy.Default;
        return new ReconciliationTolerancePolicy(command.QuantityTolerance ?? defaults.Quantity,
            command.UnitPriceTolerance ?? defaults.UnitPrice,
            command.TaxTolerance ?? defaults.Tax,
            command.LineTotalTolerance ?? defaults.LineTotal,
            command.DocumentTotalTolerance ?? defaults.DocumentTotal);
    }
}

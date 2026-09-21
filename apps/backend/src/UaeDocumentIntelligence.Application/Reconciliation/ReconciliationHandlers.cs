using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record LinkReconciliationDocumentCommand(string Role, Guid DocumentId,
    Guid ReviewSessionId);
public sealed record EvaluateReconciliationCaseCommand(decimal? QuantityTolerance = null,
    decimal? UnitPriceTolerance = null, decimal? TaxTolerance = null,
    decimal? LineTotalTolerance = null, decimal? DocumentTotalTolerance = null);
public sealed record ReconciliationFieldSourceView(Guid Id, string Key, int Occurrence,
    string ValueType, string Value, Guid? MachineFieldValueId, Guid? ReviewActionId,
    int? Page, string? Quote);
public sealed record FindingResolutionView(Guid Id, int SequenceNumber, string Action,
    Guid ActorId, string Reason, DateTimeOffset CreatedAt);
public sealed record ReconciliationFindingView(Guid Id, string RuleCode, string RuleVersion,
    string Severity, string? LeftRole, string? RightRole, string? FieldKey,
    int? Occurrence, Guid? LeftFieldSnapshotId, Guid? RightFieldSnapshotId,
    string? LeftValue, string? RightValue, decimal? Difference,
    decimal? Tolerance, string Explanation, IReadOnlyList<FindingResolutionView> Resolutions);
public sealed record ReconciliationRunView(Guid Id, int RunNumber, string Status,
    string RuleSetVersion, IReadOnlyList<ReconciliationFieldSourceView> Fields,
    IReadOnlyList<ReconciliationFindingView> Findings);
public sealed record ReconciliationDocumentLinkView(Guid Id, string Role, Guid DocumentId,
    Guid ReviewSessionId, Guid ExtractionRunId, string Sha256);
public sealed record ReconciliationCaseView(Guid Id, string Name, string Status,
    Guid ConcurrencyToken, IReadOnlyList<ReconciliationDocumentLinkView> Documents,
    IReadOnlyList<ReconciliationRunView> Runs);

public sealed class CreateReconciliationCaseHandler(IReconciliationRepository repository,
    IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(string name,
        CancellationToken cancellationToken)
    {
        var item = new ReconciliationCase(Guid.NewGuid(), execution.OrganizationId,
            execution.ActorId, name, clock.UtcNow);
        await repository.AddAsync(item, cancellationToken);
        await SaveOrThrow(cancellationToken);
        return ReconciliationView.From(item);
    }

    private async Task SaveOrThrow(CancellationToken cancellationToken)
    {
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome != ReconciliationSaveOutcome.Saved)
            throw new ReconciliationConflictException("reconciliation_create_conflict",
                "The reconciliation case could not be created.");
    }
}

public sealed class GetReconciliationCaseHandler(IReconciliationRepository repository,
    IExecutionContext execution)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        return ReconciliationView.From(item);
    }
}

public sealed class LinkReconciliationDocumentHandler(IReconciliationRepository repository,
    IDocumentRepository documents, IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, Guid concurrencyToken,
        LinkReconciliationDocumentCommand command, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        RequireVersion(item, concurrencyToken);
        var role = ParseRole(command.Role);
        var document = await documents.GetForReviewAsync(execution.OrganizationId,
            command.DocumentId, cancellationToken) ?? throw new ReconciliationNotFoundException();
        var session = RequireCompletedReview(document, command.ReviewSessionId);
        var run = document.ExtractionRuns.Single(extraction => extraction.Id == session.ExtractionRunId);
        RequireDocumentType(run.DocumentType, role);

        if (item.DocumentLinks.Any(link => link.Role == role))
            item.ReplaceReviewedDocument(Guid.NewGuid(), role, document.Id, session.Id,
                run.Id, document.Sha256, clock.UtcNow);
        else
            item.LinkReviewedDocument(Guid.NewGuid(), role, document.Id, session.Id,
                run.Id, document.Sha256, clock.UtcNow);
        await SaveOrThrow(cancellationToken);
        return ReconciliationView.From(item);
    }

    private async Task SaveOrThrow(CancellationToken cancellationToken)
    {
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == ReconciliationSaveOutcome.ConcurrencyConflict)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (outcome == ReconciliationSaveOutcome.ConstraintConflict)
            throw new ReconciliationConflictException("reconciliation_link_conflict",
                "The reviewed document could not be linked.");
    }

    internal static ReconciliationDocumentRole ParseRole(string value) =>
        Enum.TryParse<ReconciliationDocumentRole>(value, true, out var role) && Enum.IsDefined(role)
            ? role
            : throw new ReconciliationValidationException("invalid_document_role",
                "Role must be Quotation, PurchaseOrder, or Invoice.");

    internal static ReviewSession RequireCompletedReview(Document document, Guid reviewSessionId)
    {
        var session = document.ReviewSessions.SingleOrDefault(review => review.Id == reviewSessionId)
            ?? throw new ReconciliationNotFoundException();
        if (session.Status != ReviewStatus.Completed)
            throw new ReconciliationValidationException("review_not_completed",
                "A completed review revision is required for reconciliation.");
        return session;
    }

    private static void RequireDocumentType(string? documentType,
        ReconciliationDocumentRole role)
    {
        var normalized = documentType?.Trim().Replace('-', '_').ToLowerInvariant();
        var valid = role switch
        {
            ReconciliationDocumentRole.Quotation => normalized is "quotation" or "quote",
            ReconciliationDocumentRole.PurchaseOrder => normalized is "purchase_order" or "po",
            ReconciliationDocumentRole.Invoice => normalized == "invoice",
            _ => false
        };
        if (!valid)
            throw new ReconciliationValidationException("document_role_mismatch",
                "The reviewed document type does not match the requested reconciliation role.");
    }

    private static void RequireVersion(ReconciliationCase item, Guid token)
    {
        if (token == Guid.Empty || item.ConcurrencyToken != token)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
    }
}

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
        var snapshotIds = item.DocumentLinks.ToDictionary(link => link.Role, _ => Guid.NewGuid());
        var run = item.BeginEvaluation(Guid.NewGuid(), snapshotIds,
            ReconciliationRuleEngine.RuleSetVersion, policy, clock.UtcNow);

        foreach (var link in item.DocumentLinks)
        {
            var document = await documents.GetForReviewAsync(execution.OrganizationId,
                link.DocumentId, cancellationToken) ?? throw new ReconciliationNotFoundException();
            var session = LinkReconciliationDocumentHandler.RequireCompletedReview(document,
                link.ReviewSessionId);
            if (session.ExtractionRunId != link.ExtractionRunId || document.Sha256 != link.Sha256)
                throw new ReconciliationConflictException("reconciliation_source_changed",
                    "A linked reconciliation source no longer matches its pinned review.");
            ProjectReviewedValues(run, link.Role, document, session, clock.UtcNow);
        }

        new ReconciliationRuleEngine().Evaluate(run, Guid.NewGuid, clock.UtcNow);
        item.CompleteEvaluation(run, clock.UtcNow);
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == ReconciliationSaveOutcome.ConcurrencyConflict)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (outcome == ReconciliationSaveOutcome.ConstraintConflict)
            throw new ReconciliationConflictException("reconciliation_evaluation_conflict",
                "The reconciliation evaluation could not be saved atomically.");
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
            if (action?.Action == ReviewActionType.Cleared) continue;
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

public sealed record ResolveFindingCommand(string Action, string Reason);

public sealed class ResolveReconciliationFindingHandler(IReconciliationRepository repository,
    IExecutionContext execution, IClock clock)
{
    public async Task<ReconciliationCaseView> HandleAsync(Guid caseId, Guid runId,
        Guid findingId, Guid concurrencyToken, ResolveFindingCommand command,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(execution.OrganizationId, caseId, cancellationToken)
            ?? throw new ReconciliationNotFoundException();
        if (concurrencyToken == Guid.Empty || item.ConcurrencyToken != concurrencyToken)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (!Enum.TryParse<FindingResolutionAction>(command.Action, true, out var action) ||
            !Enum.IsDefined(action))
            throw new ReconciliationValidationException("invalid_resolution_action",
                "Resolution action must be Resolved, Accepted, or Dismissed.");
        try
        {
            item.ResolveFinding(runId, findingId, Guid.NewGuid(), execution.ActorId,
                action, command.Reason, clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw new ReconciliationNotFoundException();
        }
        catch (ArgumentException exception)
        {
            throw new ReconciliationValidationException("invalid_resolution_reason",
                exception.Message);
        }
        var outcome = await repository.SaveChangesAsync(cancellationToken);
        if (outcome == ReconciliationSaveOutcome.ConcurrencyConflict)
            throw new ReconciliationPreconditionException("stale_reconciliation_version",
                "The reconciliation case changed. Reload it before retrying.");
        if (outcome == ReconciliationSaveOutcome.ConstraintConflict)
            throw new ReconciliationConflictException("resolution_conflict",
                "The finding resolution could not be saved.");
        return ReconciliationView.From(item);
    }
}

internal static class ReconciliationView
{
    public static ReconciliationCaseView From(ReconciliationCase item) => new(item.Id,
        item.Name, item.Status.ToString(), item.ConcurrencyToken,
        item.DocumentLinks.OrderBy(link => link.Role).Select(link =>
            new ReconciliationDocumentLinkView(link.Id, link.Role.ToString(), link.DocumentId,
                link.ReviewSessionId, link.ExtractionRunId, link.Sha256)).ToList(),
        item.Runs.OrderByDescending(run => run.RunNumber).Select(run =>
            new ReconciliationRunView(run.Id, run.RunNumber, run.Status.ToString(),
                run.RuleSetVersion, run.DocumentSnapshots.OrderBy(snapshot => snapshot.Role)
                    .SelectMany(snapshot => snapshot.Fields.OrderBy(field => field.FieldKey)
                        .ThenBy(field => field.OccurrenceIndex).Select(field =>
                            new ReconciliationFieldSourceView(field.Id, field.FieldKey,
                                field.OccurrenceIndex, field.ValueType.ToString(), field.ValueText,
                                field.MachineFieldValueId, field.ReviewActionId,
                                field.PageNumber, field.QuotedText))).ToList(),
                run.Findings.Select(finding => new ReconciliationFindingView(finding.Id,
                    finding.RuleCode, finding.RuleVersion, finding.Severity.ToString(),
                    finding.LeftRole?.ToString(), finding.RightRole?.ToString(),
                    finding.FieldKey, finding.OccurrenceIndex,
                    finding.LeftFieldSnapshotId, finding.RightFieldSnapshotId,
                    finding.LeftValue, finding.RightValue, finding.Difference, finding.Tolerance,
                    finding.Explanation, finding.Resolutions.OrderBy(resolution =>
                        resolution.SequenceNumber).Select(resolution =>
                            new FindingResolutionView(resolution.Id,
                                resolution.SequenceNumber, resolution.Action.ToString(),
                                resolution.ActorId, resolution.Reason,
                                resolution.CreatedAtUtc)).ToList())).ToList())).ToList());
}

public abstract class ReconciliationWorkflowException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
public sealed class ReconciliationNotFoundException()
    : ReconciliationWorkflowException("reconciliation_not_found",
        "The reconciliation case or source was not found.");
public sealed class ReconciliationValidationException(string code, string message)
    : ReconciliationWorkflowException(code, message);
public sealed class ReconciliationConflictException(string code, string message)
    : ReconciliationWorkflowException(code, message);
public sealed class ReconciliationPreconditionException(string code, string message)
    : ReconciliationWorkflowException(code, message);

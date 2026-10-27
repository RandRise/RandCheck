using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

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
                                field.PageNumber, field.QuotedText, field.SourceKind?.ToString(), snapshot.Id, snapshot.Role.ToString()))).ToList(),
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
                                resolution.CreatedAtUtc)).ToList())).ToList(),
                run.Checks.Select(check => new ReconciliationCheckView(check.Id, check.Code, check.FieldKey,
                    check.OccurrenceIndex, check.LineIdentity, check.Status.ToString(), check.ReasonCode,
                    check.Explanation, check.LeftFieldSnapshotId, check.RightFieldSnapshotId, check.LeftValue,
                    check.RightValue, check.SignedDifference, check.Tolerance, check.Unit, check.Currency,
                    check.Basis, check.FindingId)).ToList(), Coverage(run),
                run.DocumentSnapshots.OrderBy(source => source.Role).Select(source => new ReconciliationSourceStateView(
                    source.Id, source.Role.ToString(), source.DocumentId, source.ReviewSessionId, source.ExtractionRunId,
                    source.Sha256, source.SourceOutcome, source.SourceProvider, source.SourceAdapterVersion,
                    source.SourceModelName, source.SourceModelVersion, source.SourceWarningsJson,
                    source.ExtractedRowCount, source.RowCoverageStatus)).ToList(),
                new ReconciliationTolerancePolicy(run.QuantityTolerance, run.UnitPriceTolerance, run.TaxTolerance,
                    run.LineTotalTolerance, run.DocumentTotalTolerance), run.OrderScope?.ToString(),
                run.EvaluationActorId, run.CreatedAtUtc)).ToList(), item.Reference, item.Category.ToString(), item.WorkflowKind.ToString(),
        new[] { ReconciliationDocumentRole.Invoice, ReconciliationDocumentRole.PurchaseOrder }
            .Where(role => item.GetDraftDocumentId(role) is not null)
            .Select(role => new ReconciliationDraftDocumentView(role.ToString(), item.GetDraftDocumentId(role)!.Value)).ToList());

    private static ReconciliationCoverageView Coverage(ReconciliationRun run)
    {
        if (!InvoicePurchaseOrderRuleEngine.IsPairPolicy(run.RuleSetVersion))
            return new("NotRecorded", 0, 0, 0, "Legacy runs did not record explicit check coverage.");
        var unavailable = run.Checks.Count(check => check.Status == ReconciliationCheckStatus.CouldNotCheck);
        var differences = run.Checks.Count(check => check.Status == ReconciliationCheckStatus.DifferenceFound);
        var passed = run.Checks.Count(check => check.Status == ReconciliationCheckStatus.NoDifference);
        // CompletedChecks retains its original API meaning: checks that ran, including differences.
        // Decisions are separate records and must never change these frozen outcomes.
        var completed = passed + differences;
        var complete = run.Status == ReconciliationRunStatus.Completed && run.Checks.Count > 0 && completed == run.Checks.Count;
        var outcome = differences > 0 ? "DifferencesFound" : complete ? "AllRequiredChecksPassed" : "ChecksIncomplete";
        return new(complete ? "Complete" : "Incomplete", completed, differences, unavailable,
            differences > 0 ? "Differences found. Read any checks that could not run as well."
                : complete ? "All required checks passed for these saved document versions."
                : "Checks incomplete. Missing information prevents a full comparison.",
            passed, run.Checks.Count, outcome);
    }
}

using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal static class ExtractionResultContract
{
    public static void Validate(WorkerExtractionRequest request, ExtractionResult result)
    {
        if (!WarningsAreOrdered(result.Warnings))
            throw new InvalidDataException("Extraction warnings are not deterministically ordered.");
        var now = DateTimeOffset.UnixEpoch;
        var organizationId = Guid.NewGuid();
        var document = new Document(Guid.NewGuid(), organizationId, Guid.NewGuid(), "probe.bin",
            "probe/source.bin", request.DeclaredContentType, request.Bytes, request.Sha256, now);
        var run = document.QueueExtraction(Guid.NewGuid(), 1, request.ProviderAlias,
            result.Provider.AdapterVersion, request.SchemaVersion, request.CorrelationId, now);
        run.Start(now);
        Apply(run, result, now);
        document.RecordExtractionSuccess(run, now);
    }

    public static void Apply(ExtractionRun run, ExtractionResult result, DateTimeOffset now)
    {
        foreach (var item in result.Pages.OrderBy(page => page.PageNumber))
            run.AddPage(Guid.NewGuid(), item.PageNumber, item.CanonicalText, item.RotationDegrees);
        foreach (var item in result.Fields)
        {
            var field = run.AddField(Guid.NewGuid(), item.Key, item.Occurrence, item.ValueType,
                item.Value, item.Confidence, now, item.NormalizedValue);
            foreach (var evidence in item.Evidence)
                run.AddEvidence(field, Guid.NewGuid(), evidence.PageNumber, evidence.Quote,
                    evidence.ByteStart, evidence.ByteEnd, evidence.BoundingPolygon, evidence.CoordinateUnit);
        }
        foreach (var warning in result.Warnings)
            run.AddWarning(Guid.NewGuid(), warning.Code, warning.Severity, warning.SafeSummary,
                warning.PageNumber, warning.FieldKey, warning.Occurrence);
        run.RecordProviderMetrics(result.Metrics.ProviderDurationMs, result.Metrics.PageCount,
            result.Metrics.EstimatedCost, result.Metrics.CostCurrency, result.Metrics.PriceBasis);
        if (result.Outcome == ExtractionOutcome.Ready)
            run.Succeed(result.DocumentType, result.Language, now,
                result.Provider.Model, result.Provider.ModelVersion);
        else
            run.CompletePartial(result.DocumentType, result.Language, now,
                result.Provider.Model, result.Provider.ModelVersion);
    }

    private static bool WarningsAreOrdered(IReadOnlyList<ExtractionWarningResult> warnings)
    {
        for (var index = 1; index < warnings.Count; index++)
            if (Compare(warnings[index - 1], warnings[index]) > 0) return false;
        return true;
    }

    private static int Compare(ExtractionWarningResult left, ExtractionWarningResult right)
    {
        var result = left.Severity.CompareTo(right.Severity);
        if (result != 0) return result;
        result = Nullable.Compare(left.PageNumber, right.PageNumber);
        if (result != 0) return result;
        result = string.Compare(left.FieldKey, right.FieldKey, StringComparison.Ordinal);
        if (result != 0) return result;
        result = Nullable.Compare(left.Occurrence, right.Occurrence);
        return result != 0 ? result : string.Compare(left.Code, right.Code, StringComparison.Ordinal);
    }
}

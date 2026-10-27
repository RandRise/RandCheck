using System.Globalization;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.Reconciliation;

public sealed record ReconciliationCaseQuery(string? Q = null, string? Category = null,
    string? Status = null, string? CreatedFrom = null, string? CreatedTo = null, string? WorkflowKind = null)
{
    public ReconciliationCaseFilter Validate()
    {
        var search = Q?.Trim();
        if (search?.Length > 200)
            throw new ReconciliationValidationException("invalid_case_search", "Case search cannot exceed 200 characters.");
        var category = Category is null ? (ReconciliationCaseCategory?)null : CaseMetadata.Category(Category);
        ReconciliationCaseStatus? status = Status is null ? null :
            Enum.TryParse<ReconciliationCaseStatus>(Status, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == Status
                ? parsed : throw new ReconciliationValidationException("invalid_case_status", "Case status must be Draft, Evaluated, or Closed.");
        var from = Day(CreatedFrom);
        var to = Day(CreatedTo);
        if (from > to)
            throw new ReconciliationValidationException("invalid_case_dates", "Created from must not be after created to.");
        return new(search, category, status,
            from.HasValue ? new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : null,
            to.HasValue ? new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)) : null,
            WorkflowKind is null ? null : CaseMetadata.Workflow(WorkflowKind));
    }

    private static DateOnly? Day(string? value) => value is null ? null :
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day : throw new ReconciliationValidationException("invalid_case_dates", "Created dates must use YYYY-MM-DD (UTC).");
}

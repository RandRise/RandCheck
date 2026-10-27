using UaeDocumentIntelligence.Domain.Common;

namespace UaeDocumentIntelligence.Domain.Model;

public enum InvoiceOrderScope { Unknown, FullOrder, Partial }
public enum ReviewedFieldSourceKind { Unchecked, Confirmed, Corrected, Added, Cleared }
public enum ReconciliationCheckStatus { NoDifference, DifferenceFound, CouldNotCheck }

public sealed class ReconciliationCheck
{
    private ReconciliationCheck() { }

    internal ReconciliationCheck(Guid id, Guid organizationId, Guid runId, string code,
        string fieldKey, int? occurrenceIndex, string? lineIdentity, ReconciliationCheckStatus status,
        string reasonCode, string explanation, Guid? leftFieldSnapshotId, Guid? rightFieldSnapshotId,
        string? leftValue, string? rightValue, decimal? signedDifference, decimal? tolerance,
        string? unit, string? currency, string? basis, Guid? findingId, DateTimeOffset now)
    {
        if (id == Guid.Empty || organizationId == Guid.Empty || runId == Guid.Empty)
            throw new ArgumentException("Check, organization and run IDs are required.");
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (occurrenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        if (status == ReconciliationCheckStatus.CouldNotCheck && signedDifference.HasValue)
            throw new ArgumentException("An unavailable check cannot contain computed arithmetic.");
        if (status != ReconciliationCheckStatus.NoDifference && !findingId.HasValue)
            throw new ArgumentException("Differences and unavailable checks require a finding.");
        if (tolerance.HasValue && !SupplierInvoiceNumber.IsPersistableTolerance(tolerance.Value))
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (signedDifference is < -999999999999999999.999999m or > 999999999999999999.999999m)
            throw new ArgumentOutOfRangeException(nameof(signedDifference));
        if (signedDifference.HasValue && ((decimal.GetBits(signedDifference.Value)[3] >> 16) & 255) > 6)
            throw new ArgumentOutOfRangeException(nameof(signedDifference), "Signed differences must fit storage without rounding.");
        Id = id;
        OrganizationId = organizationId;
        ReconciliationRunId = runId;
        Code = Guard.Required(code, nameof(code), 50);
        FieldKey = Guard.Required(fieldKey, nameof(fieldKey), 100);
        OccurrenceIndex = occurrenceIndex;
        LineIdentity = lineIdentity;
        Status = status;
        ReasonCode = Guard.Required(reasonCode, nameof(reasonCode), 100);
        Explanation = Guard.Required(explanation, nameof(explanation), 1000);
        LeftFieldSnapshotId = leftFieldSnapshotId;
        RightFieldSnapshotId = rightFieldSnapshotId;
        LeftValue = leftValue;
        RightValue = rightValue;
        SignedDifference = signedDifference;
        Tolerance = tolerance;
        Unit = unit;
        Currency = currency;
        Basis = basis;
        FindingId = findingId;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ReconciliationRunId { get; private set; }
    public string Code { get; private set; } = "";
    public string FieldKey { get; private set; } = "";
    public int? OccurrenceIndex { get; private set; }
    public string? LineIdentity { get; private set; }
    public ReconciliationCheckStatus Status { get; private set; }
    public string ReasonCode { get; private set; } = "";
    public string Explanation { get; private set; } = "";
    public Guid? LeftFieldSnapshotId { get; private set; }
    public Guid? RightFieldSnapshotId { get; private set; }
    public string? LeftValue { get; private set; }
    public string? RightValue { get; private set; }
    public decimal? SignedDifference { get; private set; }
    public decimal? Tolerance { get; private set; }
    public string? Unit { get; private set; }
    public string? Currency { get; private set; }
    public string? Basis { get; private set; }
    public Guid? FindingId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}

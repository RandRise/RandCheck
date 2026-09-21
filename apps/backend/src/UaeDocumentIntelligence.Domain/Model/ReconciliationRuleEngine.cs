using System.Globalization;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Domain.Model;

public sealed partial class ReconciliationRuleEngine
{
    public const string RuleSetVersion = "1.0";
    private static readonly ReconciliationDocumentRole[] RequiredRoles =
    [
        ReconciliationDocumentRole.Quotation,
        ReconciliationDocumentRole.PurchaseOrder,
        ReconciliationDocumentRole.Invoice
    ];
    private static readonly (ReconciliationDocumentRole Left, ReconciliationDocumentRole Right)[] ChainPairs =
    [
        (ReconciliationDocumentRole.Quotation, ReconciliationDocumentRole.PurchaseOrder),
        (ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice)
    ];
    private static readonly string[] LineKeys =
    [
        "line_item_code", "line_description", "line_quantity", "line_unit_price",
        "line_tax", "line_total"
    ];

    public void Evaluate(ReconciliationRun run, Func<Guid> nextId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(nextId);
        if (run.Status != ReconciliationRunStatus.Running)
            throw new InvalidOperationException("Only a running reconciliation run can be evaluated.");
        if (!string.Equals(run.RuleSetVersion, RuleSetVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("The reconciliation run uses an unsupported rule set.");

        var documents = run.DocumentSnapshots.ToDictionary(snapshot => snapshot.Role);
        AddMissingRoleFindings(run, documents, nextId, now);
        AddDuplicateSourceFindings(run, documents, nextId, now);

        foreach (var pair in AvailableIdentityPairs(documents))
        {
            CompareIdentity(run, pair.Left, pair.Right, "supplier_trn", "SUP-001",
                NormalizeIdentifier, documents, nextId, now);
            CompareIdentity(run, pair.Left, pair.Right, "currency", "CUR-001",
                NormalizeCurrency, documents, nextId, now);
        }

        CompareNumber(run, ReconciliationDocumentRole.Quotation,
            ReconciliationDocumentRole.PurchaseOrder, "total", "AMT-001",
            run.DocumentTotalTolerance, documents, nextId, now);
        CompareNumber(run, ReconciliationDocumentRole.PurchaseOrder,
            ReconciliationDocumentRole.Invoice, "total", "AMT-002",
            run.DocumentTotalTolerance, documents, nextId, now);
        CompareOptionalNumber(run, ReconciliationDocumentRole.PurchaseOrder,
            ReconciliationDocumentRole.Invoice, "vat", "TAX-001",
            run.TaxTolerance, documents, nextId, now);

        foreach (var pair in ChainPairs)
            CompareLines(run, pair.Left, pair.Right, documents, nextId, now);
    }

    private static void AddMissingRoleFindings(ReconciliationRun run,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now)
    {
        var available = RequiredRoles.First(role => documents.ContainsKey(role));
        foreach (var missing in RequiredRoles.Where(role => !documents.ContainsKey(role)))
            run.AddFinding(nextId(), "DOC-001", RuleSetVersion,
                ReconciliationFindingSeverity.Warning, available, missing, null, null,
                null, null, available.ToString(), null, null, null,
                $"Required {DisplayRole(missing)} document is not linked.", now);
    }

    private static void AddDuplicateSourceFindings(ReconciliationRun run,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now)
    {
        var ordered = documents.Values.OrderBy(document => document.Role).ToArray();
        for (var leftIndex = 0; leftIndex < ordered.Length; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < ordered.Length; rightIndex++)
        {
            var left = ordered[leftIndex];
            var right = ordered[rightIndex];
            if (left.DocumentId != right.DocumentId &&
                !string.Equals(left.Sha256, right.Sha256, StringComparison.Ordinal)) continue;
            run.AddFinding(nextId(), "DOC-002", RuleSetVersion,
                ReconciliationFindingSeverity.Error, left.Role, right.Role, null, null,
                null, null, left.Sha256, right.Sha256, null, null,
                $"{DisplayRole(left.Role)} and {DisplayRole(right.Role)} use the same source document.", now);
        }
    }

    private static IEnumerable<(ReconciliationDocumentRole Left, ReconciliationDocumentRole Right)>
        AvailableIdentityPairs(IReadOnlyDictionary<ReconciliationDocumentRole,
            ReconciliationDocumentSnapshot> documents)
    {
        var yielded = false;
        foreach (var pair in ChainPairs.Where(pair =>
            documents.ContainsKey(pair.Left) && documents.ContainsKey(pair.Right)))
        {
            yielded = true;
            yield return pair;
        }
        if (!yielded && documents.ContainsKey(ReconciliationDocumentRole.Quotation) &&
            documents.ContainsKey(ReconciliationDocumentRole.Invoice))
            yield return (ReconciliationDocumentRole.Quotation, ReconciliationDocumentRole.Invoice);
    }

    private static void CompareIdentity(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        string key, string ruleCode, Func<string, string> normalize,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now)
    {
        if (!TryGetComparableFields(run, leftRole, rightRole, key, 0, documents,
            nextId, now, out var left, out var right)) return;
        if (string.Equals(normalize(left.ValueText), normalize(right.ValueText),
            StringComparison.Ordinal)) return;
        run.AddFinding(nextId(), ruleCode, RuleSetVersion,
            ReconciliationFindingSeverity.Error, leftRole, rightRole, left.Id, right.Id,
            key, 0, left.ValueText, right.ValueText, null, null,
            $"{DisplayKey(key)} differs: {DisplayRole(leftRole)} is '{left.ValueText}' and " +
            $"{DisplayRole(rightRole)} is '{right.ValueText}'.", now);
    }

    private static void CompareNumber(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        string key, string ruleCode, decimal tolerance,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now) => CompareNumberCore(run, leftRole,
            rightRole, key, 0, ruleCode, tolerance, false, documents, nextId, now);

    private static void CompareOptionalNumber(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        string key, string ruleCode, decimal tolerance,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now) => CompareNumberCore(run, leftRole,
            rightRole, key, 0, ruleCode, tolerance, true, documents, nextId, now);

    private static void CompareNumberCore(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        string key, int occurrence, string ruleCode, decimal tolerance, bool optional,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now)
    {
        if (!documents.TryGetValue(leftRole, out var leftDocument) ||
            !documents.TryGetValue(rightRole, out var rightDocument)) return;
        var left = Field(leftDocument, key, occurrence);
        var right = Field(rightDocument, key, occurrence);
        if (optional && left is null && right is null) return;
        if (!TryGetComparableFields(run, leftRole, rightRole, key, occurrence,
            documents, nextId, now, out left, out right)) return;
        if (!TryDecimal(left.ValueText, out var leftNumber) ||
            !TryDecimal(right.ValueText, out var rightNumber))
        {
            AddDataFinding(run, leftRole, rightRole, key, occurrence, left, right,
                "A reviewed value is not a valid invariant decimal.", nextId, now);
            return;
        }
        var difference = Math.Abs(leftNumber - rightNumber);
        if (difference <= tolerance) return;
        run.AddFinding(nextId(), ruleCode, RuleSetVersion,
            ReconciliationFindingSeverity.Error, leftRole, rightRole, left.Id, right.Id,
            key, occurrence, left.ValueText, right.ValueText, difference, tolerance,
            $"{DisplayKey(key)} differs by {difference:0.######}; the allowed absolute tolerance is " +
            $"{tolerance:0.######}. {DisplayRole(leftRole)} is {left.ValueText} and " +
            $"{DisplayRole(rightRole)} is {right.ValueText}.", now);
    }

    private static void CompareLines(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now)
    {
        if (!documents.TryGetValue(leftRole, out var leftDocument) ||
            !documents.TryGetValue(rightRole, out var rightDocument)) return;
        var leftLines = BuildLines(run, leftDocument, nextId, now);
        var rightLines = BuildLines(run, rightDocument, nextId, now);
        foreach (var identity in leftLines.Keys.Union(rightLines.Keys).OrderBy(value => value,
            StringComparer.Ordinal))
        {
            var hasLeft = leftLines.TryGetValue(identity, out var leftLine);
            var hasRight = rightLines.TryGetValue(identity, out var rightLine);
            if (!hasLeft || !hasRight)
            {
                var availableLine = hasLeft ? leftLine! : rightLine!;
                var availableRole = hasLeft ? leftRole : rightRole;
                var missingRole = hasLeft ? rightRole : leftRole;
                var identityField = availableLine.Identity;
                run.AddFinding(nextId(), "LINE-001", RuleSetVersion,
                    ReconciliationFindingSeverity.Error, leftRole, rightRole,
                    hasLeft ? identityField.Id : null, hasRight ? identityField.Id : null,
                    identityField.FieldKey, identityField.OccurrenceIndex,
                    hasLeft ? identityField.ValueText : null,
                    hasRight ? identityField.ValueText : null, null, null,
                    $"Line '{identityField.ValueText}' from {DisplayRole(availableRole)} is missing " +
                    $"from {DisplayRole(missingRole)}.", now);
                continue;
            }
            CompareLineNumber(run, leftRole, rightRole, "line_quantity", "LINE-002",
                run.QuantityTolerance, leftLine!, rightLine!, nextId, now);
            CompareLineNumber(run, leftRole, rightRole, "line_unit_price", "LINE-003",
                run.UnitPriceTolerance, leftLine!, rightLine!, nextId, now);
            CompareLineNumber(run, leftRole, rightRole, "line_tax", "LINE-004",
                run.TaxTolerance, leftLine!, rightLine!, nextId, now, optional: true);
            CompareLineNumber(run, leftRole, rightRole, "line_total", "LINE-005",
                run.LineTotalTolerance, leftLine!, rightLine!, nextId, now);
        }
    }

    private static SortedDictionary<string, Line> BuildLines(ReconciliationRun run,
        ReconciliationDocumentSnapshot document, Func<Guid> nextId, DateTimeOffset now)
    {
        var result = new SortedDictionary<string, Line>(StringComparer.Ordinal);
        var occurrences = document.Fields.Where(field => LineKeys.Contains(field.FieldKey,
                StringComparer.Ordinal)).Select(field => field.OccurrenceIndex).Distinct().Order();
        foreach (var occurrence in occurrences)
        {
            var code = Field(document, "line_item_code", occurrence);
            var description = Field(document, "line_description", occurrence);
            var identity = code ?? description;
            if (identity is null || string.IsNullOrWhiteSpace(identity.ValueText))
            {
                AddDataFinding(run, document.Role, null, "line_identity", occurrence,
                    null, null, "Line item code or description is required.", nextId, now);
                continue;
            }
            if (!HasEvidence(identity))
            {
                AddDataFinding(run, document.Role, null, identity.FieldKey, occurrence,
                    identity, null, "Line identity has no page citation.", nextId, now);
                continue;
            }
            var normalized = code is not null
                ? $"code:{NormalizeIdentifier(code.ValueText)}"
                : $"description:{NormalizeDescription(description!.ValueText)}";
            if (result.ContainsKey(normalized))
            {
                AddDataFinding(run, document.Role, null, identity.FieldKey, occurrence,
                    identity, null, $"Line identity '{identity.ValueText}' is ambiguous.", nextId, now);
                result.Remove(normalized);
                continue;
            }
            result[normalized] = new Line(identity, occurrence,
                document.Fields.Where(field => field.OccurrenceIndex == occurrence)
                    .ToDictionary(field => field.FieldKey, StringComparer.Ordinal));
        }
        return result;
    }

    private static void CompareLineNumber(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        string key, string ruleCode, decimal tolerance, Line leftLine, Line rightLine,
        Func<Guid> nextId, DateTimeOffset now, bool optional = false)
    {
        var left = leftLine.Fields.GetValueOrDefault(key);
        var right = rightLine.Fields.GetValueOrDefault(key);
        if (optional && left is null && right is null) return;
        if (left is null || right is null || !HasEvidence(left) || !HasEvidence(right))
        {
            AddDataFinding(run, leftRole, rightRole, key, leftLine.Occurrence,
                left, right, "A line comparison value or page citation is unavailable.", nextId, now);
            return;
        }
        if (!TryDecimal(left.ValueText, out var leftNumber) ||
            !TryDecimal(right.ValueText, out var rightNumber))
        {
            AddDataFinding(run, leftRole, rightRole, key, leftLine.Occurrence,
                left, right, "A reviewed line value is not a valid invariant decimal.", nextId, now);
            return;
        }
        var difference = Math.Abs(leftNumber - rightNumber);
        if (difference <= tolerance) return;
        run.AddFinding(nextId(), ruleCode, RuleSetVersion,
            ReconciliationFindingSeverity.Error, leftRole, rightRole, left.Id, right.Id,
            key, leftLine.Occurrence, left.ValueText, right.ValueText, difference, tolerance,
            $"{DisplayKey(key)} differs by {difference:0.######}; the allowed absolute tolerance is " +
            $"{tolerance:0.######}. {DisplayRole(leftRole)} is {left.ValueText} and " +
            $"{DisplayRole(rightRole)} is {right.ValueText}.", now);
    }

    private static bool TryGetComparableFields(ReconciliationRun run,
        ReconciliationDocumentRole leftRole, ReconciliationDocumentRole rightRole,
        string key, int occurrence,
        IReadOnlyDictionary<ReconciliationDocumentRole, ReconciliationDocumentSnapshot> documents,
        Func<Guid> nextId, DateTimeOffset now,
        out ReconciliationFieldSnapshot left, out ReconciliationFieldSnapshot right)
    {
        left = null!;
        right = null!;
        if (!documents.TryGetValue(leftRole, out var leftDocument) ||
            !documents.TryGetValue(rightRole, out var rightDocument)) return false;
        var leftValue = Field(leftDocument, key, occurrence);
        var rightValue = Field(rightDocument, key, occurrence);
        if (leftValue is null || rightValue is null || !HasEvidence(leftValue) || !HasEvidence(rightValue))
        {
            AddDataFinding(run, leftRole, rightRole, key, occurrence, leftValue,
                rightValue, "A required reviewed value or page citation is unavailable.", nextId, now);
            return false;
        }
        left = leftValue;
        right = rightValue;
        return true;
    }

    private static void AddDataFinding(ReconciliationRun run,
        ReconciliationDocumentRole? leftRole, ReconciliationDocumentRole? rightRole,
        string key, int occurrence, ReconciliationFieldSnapshot? left,
        ReconciliationFieldSnapshot? right, string detail, Func<Guid> nextId,
        DateTimeOffset now)
    {
        run.AddFinding(nextId(), "DATA-001", RuleSetVersion,
            ReconciliationFindingSeverity.Warning, leftRole, rightRole,
            left?.Id, right?.Id, key, occurrence, left?.ValueText, right?.ValueText,
            null, null, $"Cannot evaluate {DisplayKey(key)} honestly. {detail}", now);
    }

    private static ReconciliationFieldSnapshot? Field(ReconciliationDocumentSnapshot document,
        string key, int occurrence) => document.Fields.SingleOrDefault(field =>
            field.FieldKey == key && field.OccurrenceIndex == occurrence);

    private static bool HasEvidence(ReconciliationFieldSnapshot field) => field.PageNumber.HasValue;
    private static bool TryDecimal(string value, out decimal result) => decimal.TryParse(value,
        NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    private static string NormalizeIdentifier(string value) => string.Concat(value.Where(char.IsLetterOrDigit))
        .ToUpperInvariant();
    private static string NormalizeCurrency(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeDescription(string value) => Whitespace().Replace(value.Trim(), " ")
        .ToUpperInvariant();
    private static string DisplayRole(ReconciliationDocumentRole role) => role switch
    {
        ReconciliationDocumentRole.PurchaseOrder => "purchase order",
        _ => role.ToString().ToLowerInvariant()
    };
    private static string DisplayKey(string key) => key.Replace('_', ' ');

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    private sealed record Line(ReconciliationFieldSnapshot Identity, int Occurrence,
        IReadOnlyDictionary<string, ReconciliationFieldSnapshot> Fields);
}

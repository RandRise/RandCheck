using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public enum CitedFactRole { Quotation, PurchaseOrder, Invoice }
public enum CitedFactOperation { Values, Difference, Maximum, LineBreakdown }
public sealed record CitedOriginalFactSpan(Guid SpanId, int PageNumber, int ByteStart, int ByteEnd, string Quote);
public sealed record CitedFactCitation(Guid IndexId, Guid DocumentId, Guid ReviewSessionId, Guid ExtractionRunId,
    Guid ChunkId, Guid ReviewActionId, string FieldKey, int Occurrence, string ReviewedValue,
    string SupportText, string ContentSha256, IReadOnlyList<CitedOriginalFactSpan> OriginalPageEvidence);
public sealed record CitedTypedFact(string FieldKey, int Occurrence, string Value, decimal? Number,
    bool Valid, CitedFactCitation Citation);
public sealed record CitedTypedDocument(Guid IndexId, Guid DocumentId, CitedFactRole? Role,
    string SourceSha256, IReadOnlyList<CitedTypedFact> Facts);
public sealed record CitedFactPlan(CitedFactOperation Operation, string FieldKey,
    IReadOnlyList<CitedFactRole> Roles, string? LineSelector = null, int? ExpectedDocumentCount = null, bool RequiresMultipleDocuments = false);
public sealed record CitedFactClaim(string Text, IReadOnlyList<CitedFactCitation> Citations);
public sealed record CitedFactOperand(Guid IndexId, CitedFactRole Role, string FieldKey, int Occurrence,
    string Value, decimal? Number, string? Currency, IReadOnlyList<CitedFactCitation> Citations);
public sealed record CitedFactCalculation(string Operation, decimal Value, string Currency,
    IReadOnlyList<Guid> OperandIndexIds, IReadOnlyList<Guid> MaximumIndexIds);
public sealed record CitedFactAnswer(string Status, string? ReasonCode, CitedFactPlan? Plan,
    IReadOnlyList<CitedFactOperand> Operands, IReadOnlyList<CitedFactClaim> Claims,
    IReadOnlyList<string> MissingParts, CitedFactCalculation? Calculation)
{
    public string PolicyVersion => "reviewed-fact-answer-v1.2";
    public string QuestionPolicyVersion => "bounded-english-fact-intents-v1.2";
    public bool PublicationAllowed => false;
    public bool GeneralSemanticSupportEstablished => false;
    public bool ModelInvoked => false;
}

/// <summary>Reconstruct facts from completed decisions, never from page labels or model-written text.</summary>
public static class CitedTypedFactProjection
{
    public const string Version = "reviewed-fact-projection-v1";
    private static readonly HashSet<string> DecimalKeys = ["total", "subtotal", "vat", "line_quantity",
        "line_unit_price", "line_tax", "line_total"];
    private static readonly HashSet<string> KnownKeys = ["quotation_number", "purchase_order_number", "invoice_number",
        "supplier_trn", "supplier_tin", "buyer_trn", "currency", "invoice_date", "due_date", "payment_terms",
        "total", "subtotal", "vat", "line_item_code", "line_description", "line_quantity", "line_unit_price",
        "line_tax", "line_total"];

    public static bool Numeric(string key) => DecimalKeys.Contains(key);
    public static bool Monetary(string key) => DecimalKeys.Contains(key) && key != "line_quantity";
    public static string IdentityKey(CitedFactRole role) => role switch
    {
        CitedFactRole.Quotation => "quotation_number", CitedFactRole.PurchaseOrder => "purchase_order_number",
        _ => "invoice_number"
    };

    public static CitedTypedDocument Project(CitedDocumentIndex index, Document document,
        ReviewSession review, ExtractionRun extraction)
    {
        if (index.Status != DocumentIndexStatus.Ready || index.DocumentId != document.Id ||
            index.OrganizationId != document.OrganizationId || index.DocumentSha256 != document.Sha256 ||
            index.ReviewSessionId != review.Id || index.ExtractionRunId != extraction.Id)
            throw Invalid();
        var snapshot = CitedIndexAssembly.CreateSnapshot(index.Id, document, review, extraction, new ChunkingProfile());
        var latest = snapshot.Facts.Where(f => f.Action != "Cleared").ToDictionary(f => f.ReviewActionId);
        var facts = new List<CitedTypedFact>();
        var seen = new HashSet<Guid>();
        foreach (var chunk in index.Chunks.Where(c => c.Kind == DocumentChunkKind.ReviewedFact))
        {
            if (chunk.OrganizationId != document.OrganizationId || chunk.IndexId != index.Id ||
                chunk.ReviewSessionId != review.Id || chunk.ExtractionRunId != extraction.Id ||
                chunk.ReviewActionId is not Guid actionId || !seen.Add(actionId) || !latest.TryGetValue(actionId, out var fact) ||
                chunk.Content != $"{fact.FieldKey}[{fact.OccurrenceIndex}]: {fact.Value}" ||
                chunk.ContentSha256 != Hash(chunk.Content)) throw Invalid();
            // The domain reconstruction also checks original page bytes, review identity and evidence spans.
            var rebuilt = new CitedDocumentIndex(index.Id, document, review, index.ProfileName, index.ProfileVersion,
                index.CreatedAtUtc).AddReviewedFact(chunk.Id, chunk.Ordinal, review, fact.FieldKey, fact.OccurrenceIndex, extraction);
            var spans = chunk.Spans.OrderBy(x => x.Id).Select(x => new CitedOriginalFactSpan(x.Id, x.PageNumber,
                x.ByteStart, x.ByteEnd, x.Quote)).ToArray();
            var expected = rebuilt.Spans.OrderBy(x => x.Id).Select(x => new CitedOriginalFactSpan(x.Id, x.PageNumber,
                x.ByteStart, x.ByteEnd, x.Quote)).ToArray();
            if (!spans.SequenceEqual(expected)) throw Invalid();
            if (!KnownKeys.Contains(fact.FieldKey)) continue;
            var value = fact.Value ?? "";
            var valid = value.Length is > 0 and <= 1000 && value == value.Trim() && !value.Any(char.IsControl);
            decimal? number = null;
            if (DecimalKeys.Contains(fact.FieldKey))
            {
                valid &= Regex.IsMatch(value, @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]{1,6})?$", RegexOptions.CultureInvariant) &&
                    decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out _);
                if (valid)
                {
                    number = decimal.Parse(value, CultureInfo.InvariantCulture);
                    valid = Math.Abs(number.Value) <= 1_000_000_000_000m;
                }
            }
            if (fact.FieldKey is "supplier_trn" or "supplier_tin" or "buyer_trn")
                valid &= Regex.IsMatch(value, "^[0-9]{1,64}$", RegexOptions.CultureInvariant);
            if (fact.FieldKey == "currency") valid &= Regex.IsMatch(value, "^[A-Z]{3}$", RegexOptions.CultureInvariant);
            if (fact.FieldKey is "invoice_date" or "due_date")
                valid &= DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            facts.Add(new(fact.FieldKey, fact.OccurrenceIndex, value, number, valid,
                new(index.Id, document.Id, review.Id, extraction.Id, chunk.Id, actionId, fact.FieldKey,
                    fact.OccurrenceIndex, value, chunk.Content, chunk.ContentSha256, Array.AsReadOnly(spans))));
        }
        if (latest.Keys.Any(id => !seen.Contains(id))) throw Invalid();
        var roles = Enum.GetValues<CitedFactRole>().Where(r => facts.Any(f =>
            f.FieldKey == IdentityKey(r) && f.Occurrence == 0 && f.Valid)).ToArray();
        var ordered = facts.OrderBy(f => f.FieldKey, StringComparer.Ordinal).ThenBy(f => f.Occurrence).ToArray();
        return new(index.Id, document.Id, roles.Length == 1 ? roles[0] : null,
            Hash(JsonSerializer.Serialize(ordered)), Array.AsReadOnly(ordered));
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static CitedWorkflowException Invalid() => new("typed_fact_source_invalid", "Reviewed facts are unavailable.", 409);
}

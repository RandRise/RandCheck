using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record EmbeddingStorageResult(int Inserted, int Reused);
public sealed record StoredEmbeddingHit(Guid IndexId, Guid ChunkId, double Similarity);

/// <summary>Internal, canonical storage. Callers supply a saved operation, never arbitrary vectors.</summary>
public interface ICitedEmbeddingStore
{
    Task<EmbeddingStorageResult> StoreChunkOutcomeAsync(Guid operationId, CancellationToken ct);
    Task<IReadOnlyList<StoredEmbeddingHit>> SearchQueryOutcomeAsync(Guid operationId, int topK, CancellationToken ct);
}

public sealed record StoredEmbeddingInput(Guid Id, string Text, string Sha256)
{
    public override string ToString() => "Embedding input (content omitted)";
}

public sealed record PreparedEmbeddingSource(Guid OrganizationId, Guid ActorId, Guid OperationId,
    string Purpose, Guid[] IndexIds, StoredEmbeddingInput[] Inputs, int InputTokens,
    JsonElement Receipt, byte[] SourceUtf8)
{
    public override string ToString() => "Prepared embedding (content omitted)";
}

public sealed record AcceptedEmbeddingOutcome(Guid OrganizationId, Guid ActorId, Guid OperationId,
    string Purpose, Guid[] IndexIds, StoredEmbeddingInput[] Inputs, float[][] Vectors,
    int InputTokens, string ResponseSha256)
{
    public override string ToString() => "Accepted embedding outcome (content omitted)";
}

/// <summary>
/// Independently checks saved Python preparation/outcome and the raw provider reply.
/// This accepts reusable vectors only; rejection must never erase known billable usage.
/// It does not dispatch, settle spending, or authorize a source.
/// </summary>
public static class CitedEmbeddingStorage
{
    public const string Model = "text-embedding-3-small";
    public const int Dimension = 1536;
    public const string Counter = "tiktoken-0.12.0-cl100k-ordinary-v1";
    public const string Wire = "openai-embeddings-token-ids-v1";
    public const string Vocabulary = "223921b76ee99bde995b7ff738513eef100fb51d18c93597a113bcffe865b2a7";
    public const string Schema = "embedding-pilot-v1";

    public static AcceptedEmbeddingOutcome Inspect(byte[] preparation, byte[] outcome)
    {
        try
        {
            var prepared = ReadPreparation(preparation);
            var (org, actor, operation, purpose, indexes, inputs, measured, p, _) = prepared;

            using var envelope = Parse(outcome, 524288);
            var o = envelope.RootElement;
            Exact(o, "schemaVersion sourceSha256 bodySha256 bindingSha256 model counterVersion wireVersion measuredInputTokens inputTokens outputTokens accountingKnown outcome requiresReconciliation providerRequestId httpStatus responseSha256 responseUtf8 publicationAllowed");
            CheckModel(o.GetProperty("model"));
            foreach (var field in new[] { "schemaVersion", "sourceSha256", "bodySha256", "bindingSha256", "counterVersion", "wireVersion" })
                Require(o.GetProperty(field).GetString() == p.GetProperty(field).GetString());
            Require(o.GetProperty("measuredInputTokens").GetInt32() == measured &&
                o.GetProperty("inputTokens").GetInt32() == measured && o.GetProperty("outputTokens").GetInt32() == 0 &&
                o.GetProperty("accountingKnown").GetBoolean() && !o.GetProperty("requiresReconciliation").GetBoolean() &&
                !o.GetProperty("publicationAllowed").GetBoolean() && o.GetProperty("outcome").GetString() == "vectors" &&
                o.GetProperty("httpStatus").GetInt32() == 200 &&
                Regex.IsMatch(o.GetProperty("providerRequestId").GetString()!, "^req_[A-Za-z0-9_-]{1,196}$",
                    RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)));
            var raw = Convert.FromBase64String(o.GetProperty("responseUtf8").GetString()!);
            var responseHash = Digest(raw);
            Require(responseHash == o.GetProperty("responseSha256").GetString());
            using var response = Parse(raw, 262144);
            var r = response.RootElement;
            Exact(r, "object model data usage");
            Require(r.GetProperty("object").GetString() == "list" && r.GetProperty("model").GetString() == Model);
            var usage = r.GetProperty("usage");
            Exact(usage, "prompt_tokens total_tokens");
            Require(usage.GetProperty("prompt_tokens").GetInt32() == measured && usage.GetProperty("total_tokens").GetInt32() == measured);
            var vectors = new float[inputs.Length][];
            var data = r.GetProperty("data");
            Require(data.GetArrayLength() == inputs.Length);
            foreach (var item in data.EnumerateArray())
            {
                Exact(item, "object index embedding");
                var i = item.GetProperty("index").GetInt32();
                Require(item.GetProperty("object").GetString() == "embedding" && i >= 0 && i < vectors.Length && vectors[i] is null);
                var values = item.GetProperty("embedding").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                Require(values.Length == Dimension && values.All(x => double.IsFinite(x) && Math.Abs(x) <= 1));
                vectors[i] = values.Select(x => (float)x).ToArray();
                Require(vectors[i].Any(x => x != 0)); // Reject underflow to an all-zero stored vector too.
            }
            return new(org, actor, operation, purpose, indexes, inputs, vectors, measured, responseHash);
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException or ArgumentException
            or KeyNotFoundException or OverflowException or NullReferenceException)
        {
            throw new CitedWorkflowException("embedding_outcome_not_reusable", "The saved embedding outcome cannot be reused.");
        }
    }

    internal static PreparedEmbeddingSource ReadPreparation(byte[] preparation)
    {
    using var dispatch = Parse(preparation, 98304);
    var d = dispatch.RootElement;
    Exact(d, "requestUtf8 preparation");
    var sourceBytes = Convert.FromBase64String(d.GetProperty("requestUtf8").GetString()!);
    using var source = Parse(sourceBytes, 49152);
    var s = source.RootElement;
    Exact(s, "schemaVersion context model inputs");
    Require(s.GetProperty("schemaVersion").GetString() == Schema);
    CheckModel(s.GetProperty("model"));
    var context = s.GetProperty("context");
    Exact(context, "organizationId actorId operationId purpose indexIds");
    var org = Id(context.GetProperty("organizationId"));
    var actor = Id(context.GetProperty("actorId"));
    var operation = Id(context.GetProperty("operationId"));
    var purpose = context.GetProperty("purpose").GetString()!;
    Require(purpose is "chunk" or "query");
    var indexes = context.GetProperty("indexIds").EnumerateArray().Select(Id).ToArray();
    Require(indexes.Length is >= 1 and <= 20 && indexes.Distinct().Count() == indexes.Length &&
        indexes.SequenceEqual(indexes.OrderBy(x => x.ToString(), StringComparer.Ordinal)));
    var inputs = s.GetProperty("inputs").EnumerateArray().Select(x =>
    {
        Exact(x, "inputId text inputSha256");
        var text = x.GetProperty("text").GetString()!;
        var hash = x.GetProperty("inputSha256").GetString()!;
        Require(!string.IsNullOrWhiteSpace(text) && Encoding.UTF8.GetByteCount(text) <= 8000 &&
            Digest(new UTF8Encoding(false, true).GetBytes(text)) == hash);
        return new StoredEmbeddingInput(Id(x.GetProperty("inputId")), text, hash);
    }).ToArray();
    Require(inputs.Length is >= 1 and <= 4 && inputs.Select(x => x.Id).Distinct().Count() == inputs.Length &&
        (purpose != "query" || inputs.Length == 1));

    var p = d.GetProperty("preparation");
    Exact(p, "schemaVersion sourceSha256 model bodySha256 bindingSha256 wireVersion counterVersion vocabularySha256 inputTokens tokenCounts maximumOutputTokens maximumResponseBytes");
    CheckModel(p.GetProperty("model"));
    Require(p.GetProperty("schemaVersion").GetString() == Schema &&
        p.GetProperty("sourceSha256").GetString() == Digest(sourceBytes) &&
        Hash(p.GetProperty("bodySha256").GetString()) && Hash(p.GetProperty("bindingSha256").GetString()) &&
        p.GetProperty("wireVersion").GetString() == Wire && p.GetProperty("counterVersion").GetString() == Counter &&
        p.GetProperty("vocabularySha256").GetString() == Vocabulary &&
        p.GetProperty("maximumOutputTokens").GetInt32() == 0 && p.GetProperty("maximumResponseBytes").GetInt32() == 262144);
    var measured = p.GetProperty("inputTokens").GetInt32();
    var counts = p.GetProperty("tokenCounts").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    Require(measured is >= 1 and <= 8000 && counts.Length == inputs.Length &&
        counts.All(x => x is >= 1 and <= 8000) && counts.Sum() == measured);
        return new(org, actor, operation, purpose, indexes, inputs, measured, p.Clone(), sourceBytes);
    }

    public static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static bool Hash(string? s) => s is { Length: 64 } && s.All(char.IsAsciiHexDigitLower);
    internal static Guid Id(JsonElement value)
    {
        var text = value.GetString();
        Require(Guid.TryParseExact(text, "D", out var id) && id != Guid.Empty && id.ToString() == text);
        return id;
    }
    internal static void CheckModel(JsonElement m)
    {
        Exact(m, "providerAlias model version dimension distance");
        Require(m.GetProperty("providerAlias").GetString() == "openai" && m.GetProperty("model").GetString() == Model &&
            m.GetProperty("version").GetString() == Model && m.GetProperty("dimension").GetInt32() == Dimension &&
            m.GetProperty("distance").GetString() == "cosine");
    }
    internal static void Require(bool condition) { if (!condition) throw new FormatException(); }
    internal static void Exact(JsonElement value, string fields) => Require(value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Select(x => x.Name).Order().SequenceEqual(fields.Split(' ').Order()));
    internal static JsonDocument Parse(byte[] bytes, int limit)
    {
        Require(bytes.Length >= 1 && bytes.Length <= limit);
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        try { Unique(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    internal static void Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in value.EnumerateObject()) { Require(names.Add(p.Name)); Unique(p.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
    }
}

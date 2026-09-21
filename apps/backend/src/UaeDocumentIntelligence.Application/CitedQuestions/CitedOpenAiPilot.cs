using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record OpenAiPreparation(string ProtocolVersion, string GenerationSha256, string CountingSha256,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReasoningEffort = null);
public sealed record OpenAiOutcome
{
    public required string GenerationSha256 { get; init; }
    public bool AccountingKnown { get; init; }
    public string? CountRequestId { get; init; }
    public byte[]? CountResponseUtf8 { get; init; }
    public int? MeasuredInputTokens { get; init; }
    public string? ProviderRequestId { get; init; }
    public string? ResponseId { get; init; }
    public string? ResponseSha256 { get; init; }
    public byte[]? ResponseUtf8 { get; init; }
    public int CachedInputTokens { get; init; }
    public int ReasoningTokens { get; init; }
    public string? ProviderStatus { get; init; }
    public int? HttpStatus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportErrorCode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransportFailureKind { get; init; }
    public override string ToString() => "OpenAI outcome (content omitted)";
}

// Explicit local synthetic runner only. Default API configuration never selects this profile.
public static class CitedOpenAiPilot
{
    public const string CounterVersion = "openai-count-after-reservation-v1";
    public const string ProtocolVersion = "openai-cited-responses-v1";
    public static CitedGenerationRate Rate => new("openai", "gpt-5-nano", "gpt-5-nano-2025-08-07",
        "openai-nano-standard-usd-2026-09-10", new(2026, 9, 10, 0, 0, 0, TimeSpan.Zero),
        new(2026, 9, 11, 0, 0, 0, TimeSpan.Zero), .05m, .40m);

    public static bool ValidPreparation(PrivateAnswerPreparation p, bool lowReasoning = false) =>
        p.CounterVersion == CounterVersion && p.InputTokens == CitedGenerationBudgetPolicy.MaximumInputTokens &&
        p.OpenAi is { } wire && wire.ProtocolVersion == ProtocolVersion &&
        wire.ReasoningEffort == (lowReasoning ? "low" : null) &&
        Hash(wire.GenerationSha256) && Hash(wire.CountingSha256);

    public static (decimal? Cost, string? Evidence, bool Freeze) Inspect(PrivateAnswerResult result,
        PrivateAnswerPreparation preparation)
    {
        try
        {
            var value = result.OpenAi ?? throw new FormatException();
            if (value.TransportErrorCode is not (null or "invalid_openai_request" or "openai_transport_uncertain" or
                    "openai_response_rejected" or "openai_http_rejected" or "unclassified") ||
                value.TransportFailureKind is not (null or "deadline" or "http_timeout" or "network" or "protocol" or "other"))
                throw new FormatException();
            if (value.GenerationSha256 != preparation.OpenAi?.GenerationSha256 ||
                result.InputTokens < 0 || result.OutputTokens < 0 || result.ProposalUtf8.Length > 65536 ||
                value.CountResponseUtf8?.Length > 1024 || value.ResponseUtf8?.Length > 262144 ||
                result.Outcome is not ("completed" or "provider_refusal" or "provider_incomplete" or
                    "invalid_output" or "transport_uncertain" or "accounting_unavailable")) throw new FormatException();
            if (value.ResponseUtf8 is not null && Digest(value.ResponseUtf8) != value.ResponseSha256) throw new FormatException();
            if (!value.AccountingKnown) return (null, null, true); // Never infer a zero bill from absent usage.
            if (!Id(value.ProviderRequestId, "req_") || !Id(value.ResponseId, "resp_") ||
                !Id(value.CountRequestId, "req_") || value.ResponseUtf8 is null || value.CountResponseUtf8 is null ||
                value.MeasuredInputTokens is not (>= 1 and <= 8000) ||
                value.CachedInputTokens < 0 || value.CachedInputTokens > result.InputTokens ||
                value.ReasoningTokens < 0 || value.ReasoningTokens > result.OutputTokens) throw new FormatException();
            using var counted = Document(value.CountResponseUtf8);
            if (counted.RootElement.GetProperty("object").GetString() != "response.input_tokens" ||
                counted.RootElement.GetProperty("input_tokens").GetInt32() != value.MeasuredInputTokens) throw new FormatException();
            using var raw = Document(value.ResponseUtf8);
            var root = raw.RootElement;
            if (preparation.OpenAi?.ReasoningEffort == "low" &&
                root.GetProperty("reasoning").GetProperty("effort").GetString() != "low") throw new FormatException();
            if (root.GetProperty("id").GetString() != value.ResponseId ||
                root.GetProperty("object").GetString() != "response" ||
                root.GetProperty("model").GetString() != Rate.ModelVersion ||
                root.GetProperty("service_tier").GetString() != "default" ||
                root.GetProperty("tools").GetArrayLength() != 0 ||
                root.GetProperty("status").GetString() != value.ProviderStatus ||
                value.ProviderStatus is not ("completed" or "incomplete" or "failed" or "cancelled")) throw new FormatException();
            var usage = root.GetProperty("usage");
            var inputs = usage.GetProperty("input_tokens_details");
            if (usage.GetProperty("input_tokens").GetInt32() != result.InputTokens ||
                usage.GetProperty("output_tokens").GetInt32() != result.OutputTokens ||
                usage.GetProperty("total_tokens").GetInt64() != (long)result.InputTokens + result.OutputTokens ||
                inputs.GetProperty("cached_tokens").GetInt32() != value.CachedInputTokens ||
                inputs.TryGetProperty("cache_write_tokens", out var writes) && writes.GetInt32() != 0 ||
                usage.GetProperty("output_tokens_details").GetProperty("reasoning_tokens").GetInt32() != value.ReasoningTokens)
                throw new FormatException();
            // Provider-reported token-rate estimate; not an independently reconciled account bill.
            var cost = decimal.Ceiling((result.InputTokens - value.CachedInputTokens) * .05m +
                value.CachedInputTokens * .005m + result.OutputTokens * .40m) / 1_000_000m;
            return (cost, "openai-response:" + value.ResponseSha256,
                result.InputTokens > value.MeasuredInputTokens || result.OutputTokens > preparation.MaximumOutputTokens);
        }
        catch { throw new CitedWorkflowException("invalid_openai_accounting", "Provider accounting was not accepted."); }
    }

    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => char.IsAsciiHexDigitLower(c));
    private static bool Id(string? value, string prefix) => value is not null &&
        value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length &&
        Regex.IsMatch(value, "^[A-Za-z0-9_-]{1,200}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static string Digest(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static JsonDocument Document(byte[] value)
    {
        var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 32 });
        try { Unique(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { if (!names.Add(property.Name)) throw new FormatException(); Unique(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
    }
}

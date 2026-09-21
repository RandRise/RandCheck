using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class AiServiceOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8001";
    public string ProviderAlias { get; set; } = "local";
    public string AdapterVersion { get; set; } = "local-1.3.0";
    public string SchemaVersion { get; set; } = "2.0";
    public string WorkerToken { get; set; } = string.Empty;
    // Standalone Phase 5 tools retain explicit local configuration. The API host
    // resolves this mode against its environment and locks enterprise defaults.
    public string AuthenticationMode { get; set; } = "Development";
    public PrivateServiceCredential ExtractionCredential { get; set; } = new();
    public PrivateServiceCredential CitedCredential { get; set; } = new();
    public PrivateServiceCredential AnswerCredential { get; set; } = new();

    public static string ResolveAuthenticationMode(string? configured, string environment)
    {
        var development = environment is "Development" or "Testing";
        var mode = string.IsNullOrEmpty(configured) ? (development ? "Development" : "Locked") : configured;
        return mode == "ServiceKeys" || mode == "Development" && development ? mode : "Locked";
    }
}

internal sealed partial class PythonExtractionClient(
    HttpClient client, IOptions<AiServiceOptions>? configuredOptions = null) : IExtractionClient
{
    private const long MaxNormalizedResultBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> RetryableCodes =
    [
        "provider_network_error", "provider_timeout", "provider_throttled", "provider_unavailable"
    ];
    private static readonly HashSet<string> PermanentCodes =
    [
        "unsupported_schema_version", "invalid_storage_key", "content_integrity_mismatch",
        "input_too_large", "page_limit_exceeded", "unsupported_media_type", "corrupt_document",
        "encrypted_document", "document_unreadable", "unsupported_document", "ocr_unavailable",
        "provider_configuration_error", "provider_authentication_failed", "provider_rejected",
        "malformed_provider_result", "request_cancelled", "internal_extraction_error"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly AiServiceOptions _options = configuredOptions?.Value ?? new AiServiceOptions();

    public string ProviderName => _options.ProviderAlias;
    public string ProviderVersion => _options.AdapterVersion;
    public string SchemaVersion => _options.SchemaVersion;

    public async Task<ExtractionResult> ExtractAsync(WorkerExtractionRequest workerRequest,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/extractions")
        {
            Content = JsonContent.Create(new AiRequest(workerRequest.SchemaVersion,
                workerRequest.JobId, workerRequest.DocumentId, workerRequest.ExtractionRunId,
                workerRequest.StorageKey, workerRequest.Sha256, workerRequest.Bytes,
                workerRequest.DeclaredContentType, workerRequest.ProviderAlias, workerRequest.Attempt,
                workerRequest.DeadlineUtc, workerRequest.CorrelationId), options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("X-Request-ID", workerRequest.CorrelationId);
        if (!PrivateServiceCredentials.TryApply(request, client, _options))
            throw new ExtractionProviderException("provider_configuration_error",
                "The private extraction service credential or transport is not configured.", false);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new ExtractionProviderException("provider_network_error",
                "The extraction provider could not be reached.", true, exception);
        }
        using (response)
        {
            var responseCorrelation = response.Headers.TryGetValues("X-Request-ID", out var ids)
                ? ids.SingleOrDefault()
                : null;
            if (response.Content.Headers.ContentLength is > MaxNormalizedResultBytes)
                throw Malformed();

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var boundedStream = new LengthLimitedReadStream(responseStream,
                MaxNormalizedResultBytes);
            if (!response.IsSuccessStatusCode)
            {
                AiErrorEnvelope? envelope = null;
                try
                {
                    envelope = await JsonSerializer.DeserializeAsync<AiErrorEnvelope>(boundedStream,
                        JsonOptions, cancellationToken);
                }
                catch (JsonException)
                {
                    // A proxy may return an empty or non-JSON transient response. Its body is never exposed.
                }
                if (envelope is not null)
                    throw ToProviderException(envelope, workerRequest, response.StatusCode,
                        responseCorrelation);
                throw TransientStatusException(response.StatusCode) ?? Malformed();
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK) throw Malformed();
            if (!string.Equals(responseCorrelation, workerRequest.CorrelationId, StringComparison.Ordinal))
                throw Malformed();
            AiResponse result;
            try
            {
                result = await JsonSerializer.DeserializeAsync<AiResponse>(boundedStream, JsonOptions,
                    cancellationToken) ?? throw Malformed();
            }
            catch (JsonException)
            {
                throw Malformed();
            }
            var mapped = MapAndValidate(result, workerRequest);
            try
            {
                ExtractionResultContract.Validate(workerRequest, mapped);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidDataException)
            {
                throw new ExtractionProviderException("malformed_provider_result",
                    "The extraction provider returned an invalid result.", false, exception);
            }
            return mapped;
        }
    }

    private ExtractionResult MapAndValidate(AiResponse result, WorkerExtractionRequest request)
    {
        if (result.Provider is null || result.Metrics is null || result.Pages is null ||
            result.Fields is null || result.Warnings is null ||
            result.SchemaVersion != request.SchemaVersion || result.SchemaVersion != SchemaVersion ||
            result.Provider.Alias != request.ProviderAlias || result.Provider.Alias != ProviderName ||
            result.Provider.AdapterVersion != ProviderVersion ||
            string.IsNullOrWhiteSpace(result.Provider.Model) || result.Provider.Model.Length > 100 ||
            string.IsNullOrWhiteSpace(result.Provider.ModelVersion) || result.Provider.ModelVersion.Length > 100 ||
            !Enum.TryParse<ExtractionOutcome>(result.Outcome, true, out var outcome) ||
            !Enum.IsDefined(outcome))
            throw Malformed();

        try
        {
            var fields = result.Fields.Select(field =>
            {
                if (!Enum.TryParse<FieldValueType>(field.ValueType, true, out var valueType) ||
                    !Enum.IsDefined(valueType)) throw Malformed();
                var evidence = field.Evidence.Select(item => new ExtractionEvidenceResult(
                    item.PageNumber, item.Quote, item.StartUtf8, item.EndUtf8,
                    item.Polygon is null ? null : JsonSerializer.Serialize(item.Polygon),
                    item.CoordinateUnit)).ToList();
                return new ExtractionField(field.Key, field.Occurrence, valueType, field.Value,
                    field.NormalizedValue, field.Confidence, evidence);
            }).ToList();
            var warnings = result.Warnings.Select(warning =>
            {
                if (!Enum.TryParse<ExtractionWarningSeverity>(warning.Severity, true, out var severity) ||
                    !Enum.IsDefined(severity)) throw Malformed();
                return new ExtractionWarningResult(warning.Code, severity, warning.SafeSummary,
                    warning.PageNumber, warning.FieldKey, warning.Occurrence);
            }).ToList();
            return new ExtractionResult(result.SchemaVersion, outcome, result.DocumentType, result.Language,
                new ExtractionProviderResult(result.Provider.Alias, result.Provider.AdapterVersion,
                    result.Provider.Model, result.Provider.ModelVersion),
                new ExtractionMetricsResult(result.Metrics.ProviderDurationMs, result.Metrics.PageCount,
                    result.Metrics.EstimatedCost, result.Metrics.CostCurrency, result.Metrics.PriceBasis),
                result.Pages.Select(page => new ExtractionPageResult(page.PageNumber,
                    page.CanonicalText, page.RotationDegrees)).ToList(), fields, warnings);
        }
        catch (ExtractionProviderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ExtractionProviderException("malformed_provider_result",
                "The extraction provider returned an invalid result.", false, exception);
        }
    }

    private ExtractionProviderException ToProviderException(AiErrorEnvelope envelope,
        WorkerExtractionRequest request, System.Net.HttpStatusCode statusCode,
        string? responseCorrelation)
    {
        var error = envelope.Error;
        if (envelope.SchemaVersion != request.SchemaVersion || error is null ||
            responseCorrelation != request.CorrelationId ||
            error.CorrelationId != request.CorrelationId || !SafeCodePattern().IsMatch(error.Code) ||
            string.IsNullOrWhiteSpace(error.Summary) || error.Summary.Length > 500 ||
            (!RetryableCodes.Contains(error.Code) && !PermanentCodes.Contains(error.Code)))
            return Malformed();
        var retryable = RetryableCodes.Contains(error.Code);
        if (error.Retryable != retryable) return Malformed();
        if (!StatusMatchesCode(statusCode, error.Code)) return Malformed();
        return new ExtractionProviderException(error.Code, error.Summary, retryable);
    }

    private static bool StatusMatchesCode(System.Net.HttpStatusCode statusCode, string code) => code switch
    {
        "provider_timeout" => statusCode is System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.GatewayTimeout,
        "provider_throttled" => statusCode == System.Net.HttpStatusCode.TooManyRequests,
        "provider_network_error" or "provider_unavailable" =>
            statusCode == System.Net.HttpStatusCode.ServiceUnavailable,
        "ocr_unavailable" => statusCode == System.Net.HttpStatusCode.ServiceUnavailable,
        "unsupported_media_type" => statusCode == System.Net.HttpStatusCode.UnsupportedMediaType,
        _ => (int)statusCode == 422
    };

    private static ExtractionProviderException? TransientStatusException(
        System.Net.HttpStatusCode statusCode) => statusCode switch
    {
        System.Net.HttpStatusCode.RequestTimeout => new ExtractionProviderException(
            "provider_timeout", "The extraction provider request timed out.", true),
        System.Net.HttpStatusCode.TooManyRequests => new ExtractionProviderException(
            "provider_throttled", "The extraction provider temporarily throttled the request.", true),
        _ when (int)statusCode >= 500 => new ExtractionProviderException(
            "provider_unavailable", "The extraction provider is temporarily unavailable.", true),
        _ => null
    };

    private static ExtractionProviderException Malformed() => new("malformed_provider_result",
        "The extraction provider returned an invalid result.");

    [GeneratedRegex("^[a-z][a-z0-9_]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeCodePattern();

    private sealed record AiRequest(
        [property: JsonPropertyName("schema_version")] string SchemaVersion,
        [property: JsonPropertyName("job_id")] Guid JobId,
        [property: JsonPropertyName("document_id")] Guid DocumentId,
        [property: JsonPropertyName("extraction_run_id")] Guid ExtractionRunId,
        [property: JsonPropertyName("storage_key")] string StorageKey,
        string Sha256,
        long Bytes,
        [property: JsonPropertyName("declared_content_type")] string DeclaredContentType,
        [property: JsonPropertyName("provider_alias")] string ProviderAlias,
        int Attempt,
        [property: JsonPropertyName("deadline_utc")] DateTimeOffset DeadlineUtc,
        [property: JsonPropertyName("correlation_id")] string CorrelationId);
    private sealed record AiProvider(string Alias,
        [property: JsonPropertyName("adapter_version")] string AdapterVersion,
        string Model, [property: JsonPropertyName("model_version")] string ModelVersion);
    private sealed record AiMetrics(
        [property: JsonPropertyName("provider_duration_ms")] long ProviderDurationMs,
        [property: JsonPropertyName("page_count")] int PageCount,
        [property: JsonPropertyName("estimated_cost")] decimal? EstimatedCost,
        [property: JsonPropertyName("cost_currency")] string? CostCurrency,
        [property: JsonPropertyName("price_basis")] string? PriceBasis);
    private sealed record AiPage([property: JsonPropertyName("page_number")] int PageNumber,
        [property: JsonPropertyName("canonical_text")] string CanonicalText,
        [property: JsonPropertyName("rotation_degrees")] short RotationDegrees);
    private sealed record AiEvidence([property: JsonPropertyName("page_number")] int PageNumber,
        string? Quote, [property: JsonPropertyName("start_utf8")] int? StartUtf8,
        [property: JsonPropertyName("end_utf8")] int? EndUtf8, decimal[][]? Polygon,
        [property: JsonPropertyName("coordinate_unit")] string? CoordinateUnit);
    private sealed record AiField(string Key, int Occurrence,
        [property: JsonPropertyName("value_type")] string ValueType, string Value,
        [property: JsonPropertyName("normalized_value")] string? NormalizedValue,
        decimal? Confidence, List<AiEvidence> Evidence);
    private sealed record AiWarning(string Code, string Severity,
        [property: JsonPropertyName("safe_summary")] string SafeSummary,
        [property: JsonPropertyName("page_number")] int? PageNumber,
        [property: JsonPropertyName("field_key")] string? FieldKey, int? Occurrence);
    private sealed record AiResponse([property: JsonPropertyName("schema_version")] string SchemaVersion,
        string Outcome, [property: JsonPropertyName("document_type")] string DocumentType,
        string Language, AiProvider Provider, AiMetrics Metrics, List<AiPage> Pages,
        List<AiField> Fields, List<AiWarning> Warnings);
    private sealed record AiErrorEnvelope(
        [property: JsonPropertyName("schema_version")] string SchemaVersion, AiError Error);
    private sealed record AiError(string Code, bool Retryable, string Summary,
        [property: JsonPropertyName("correlation_id")] string CorrelationId);

    private sealed class LengthLimitedReadStream(Stream inner, long maximumBytes) : Stream
    {
        private long _bytesRead;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Record(inner.Read(buffer, offset, count));
        public override int Read(Span<byte> buffer) => Record(inner.Read(buffer));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default) => Record(await inner.ReadAsync(buffer, cancellationToken));
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) => Record(await inner.ReadAsync(buffer, offset, count, cancellationToken));
        private int Record(int count)
        {
            _bytesRead += count;
            if (_bytesRead > maximumBytes) throw Malformed();
            return count;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
    }
}

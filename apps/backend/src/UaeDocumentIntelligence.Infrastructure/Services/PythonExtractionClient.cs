using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class PythonExtractionClient(HttpClient client) : IExtractionClient
{
    private const long MaxNormalizedResultBytes = 5 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "mock";
    public string ProviderVersion => "2.0";
    public string SchemaVersion => "2.0";

    public async Task<ExtractionResult> ExtractAsync(string absolutePath, string contentType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/extract")
        {
            Content = JsonContent.Create(new AiRequest(absolutePath, contentType))
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxNormalizedResultBytes)
            throw new InvalidDataException("AI service response exceeds the normalized-result limit.");
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var boundedStream = new LengthLimitedReadStream(responseStream,
            MaxNormalizedResultBytes);
        var result = await JsonSerializer.DeserializeAsync<AiResponse>(boundedStream, JsonOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("AI service returned no result.");
        if (!Enum.TryParse<ExtractionOutcome>(result.Outcome, true, out var outcome) ||
            !Enum.IsDefined(outcome))
            throw new InvalidDataException("AI service returned an invalid extraction outcome.");

        var fields = result.Fields.Select(field =>
        {
            if (!Enum.TryParse<FieldValueType>(field.ValueType, true, out var valueType) ||
                !Enum.IsDefined(valueType))
                throw new InvalidDataException("AI service returned an invalid field value type.");
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
                !Enum.IsDefined(severity))
                throw new InvalidDataException("AI service returned an invalid warning severity.");
            return new ExtractionWarningResult(warning.Code, severity, warning.SafeSummary,
                warning.PageNumber, warning.FieldKey, warning.Occurrence);
        }).ToList();

        return new ExtractionResult(result.SchemaVersion, outcome, result.DocumentType, result.Language,
            new ExtractionProviderResult(result.Provider.Alias, result.Provider.AdapterVersion,
                result.Provider.Model, result.Provider.ModelVersion),
            new ExtractionMetricsResult(result.Metrics.ProviderDurationMs, result.Metrics.PageCount,
                result.Metrics.EstimatedCost, result.Metrics.CostCurrency, result.Metrics.PriceBasis),
            result.Pages.Select(page => new ExtractionPageResult(page.PageNumber,
                page.CanonicalText, page.RotationDegrees)).ToList(),
            fields, warnings);
    }

    private sealed record AiRequest(string FilePath, string ContentType);
    private sealed record AiProvider(
        string Alias,
        [property: JsonPropertyName("adapter_version")] string AdapterVersion,
        string Model,
        [property: JsonPropertyName("model_version")] string ModelVersion);
    private sealed record AiMetrics(
        [property: JsonPropertyName("provider_duration_ms")] long ProviderDurationMs,
        [property: JsonPropertyName("page_count")] int PageCount,
        [property: JsonPropertyName("estimated_cost")] decimal? EstimatedCost,
        [property: JsonPropertyName("cost_currency")] string? CostCurrency,
        [property: JsonPropertyName("price_basis")] string? PriceBasis);
    private sealed record AiPage(
        [property: JsonPropertyName("page_number")] int PageNumber,
        [property: JsonPropertyName("canonical_text")] string CanonicalText,
        [property: JsonPropertyName("rotation_degrees")] short RotationDegrees);
    private sealed record AiEvidence(
        [property: JsonPropertyName("page_number")] int PageNumber,
        string? Quote,
        [property: JsonPropertyName("start_utf8")] int? StartUtf8,
        [property: JsonPropertyName("end_utf8")] int? EndUtf8,
        decimal[][]? Polygon,
        [property: JsonPropertyName("coordinate_unit")] string? CoordinateUnit);
    private sealed record AiField(
        string Key,
        int Occurrence,
        [property: JsonPropertyName("value_type")] string ValueType,
        string Value,
        [property: JsonPropertyName("normalized_value")] string? NormalizedValue,
        decimal? Confidence,
        List<AiEvidence> Evidence);
    private sealed record AiWarning(
        string Code,
        string Severity,
        [property: JsonPropertyName("safe_summary")] string SafeSummary,
        [property: JsonPropertyName("page_number")] int? PageNumber,
        [property: JsonPropertyName("field_key")] string? FieldKey,
        int? Occurrence);
    private sealed record AiResponse(
        [property: JsonPropertyName("schema_version")] string SchemaVersion,
        string Outcome,
        [property: JsonPropertyName("document_type")] string DocumentType,
        string Language,
        AiProvider Provider,
        AiMetrics Metrics,
        List<AiPage> Pages,
        List<AiField> Fields,
        List<AiWarning> Warnings);

    private sealed class LengthLimitedReadStream(Stream inner, long maximumBytes) : Stream
    {
        private long _bytesRead;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Record(inner.Read(buffer, offset, count));

        public override int Read(Span<byte> buffer) => Record(inner.Read(buffer));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            Record(await inner.ReadAsync(buffer, cancellationToken));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) =>
            Record(await inner.ReadAsync(buffer, offset, count, cancellationToken));

        private int Record(int count)
        {
            _bytesRead += count;
            if (_bytesRead > maximumBytes)
                throw new InvalidDataException("AI service response exceeds the normalized-result limit.");
            return count;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}

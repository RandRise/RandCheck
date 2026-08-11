using System.Net;
using System.Net.Http.Headers;
using System.Text;
using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace UaeDocumentIntelligence.Infrastructure.Tests;

public sealed class PythonExtractionClientTests
{
    [Fact]
    public async Task Schema_2_response_round_trips_pages_fields_evidence_warnings_and_metrics()
    {
        const string body = """
            {
              "schema_version":"2.0","outcome":"Partial","document_type":"invoice","language":"en",
              "provider":{"alias":"mock","adapter_version":"2.0","model":"mock-invoice","model_version":"v1"},
              "metrics":{"provider_duration_ms":12,"page_count":1,"estimated_cost":null,"cost_currency":null,"price_basis":null},
              "pages":[{"page_number":1,"canonical_text":"Aé ض","rotation_degrees":90}],
              "fields":[{"key":"total","occurrence":0,"value_type":"Decimal","value":"10.00","normalized_value":"10.00","confidence":null,
                "evidence":[{"page_number":1,"quote":"é ض","start_utf8":1,"end_utf8":6,"polygon":[[0.1,0.1],[0.9,0.1],[0.9,0.9],[0.1,0.9]],"coordinate_unit":"normalized"}]}],
              "warnings":[{"code":"page_ocr_degraded","severity":"Degraded","safe_summary":"One page produced incomplete OCR text.","page_number":1,"field_key":null,"occurrence":null}]
            }
            """;
        var client = CreateClient(body);

        var result = await client.ExtractAsync("safe/file.pdf", "application/pdf", CancellationToken.None);

        Assert.Equal("2.0", result.SchemaVersion);
        Assert.Equal(ExtractionOutcome.Partial, result.Outcome);
        Assert.Equal("mock-invoice", result.Provider.Model);
        Assert.Equal(12, result.Metrics.ProviderDurationMs);
        Assert.Equal(90, Assert.Single(result.Pages).RotationDegrees);
        var field = Assert.Single(result.Fields);
        Assert.Null(field.Confidence);
        Assert.Equal(FieldValueType.Decimal, field.ValueType);
        var evidence = Assert.Single(field.Evidence);
        Assert.Equal(1, evidence.ByteStart);
        Assert.Equal(6, evidence.ByteEnd);
        Assert.Contains("[0.1,0.1]", evidence.BoundingPolygon, StringComparison.Ordinal);
        Assert.Equal(ExtractionWarningSeverity.Degraded, Assert.Single(result.Warnings).Severity);
    }

    [Fact]
    public async Task Undefined_enum_text_is_rejected_without_exposing_response_body()
    {
        const string body = """
            {"schema_version":"2.0","outcome":"Ready","document_type":"invoice","language":"en",
             "provider":{"alias":"mock","adapter_version":"2.0","model":"mock","model_version":"v1"},
             "metrics":{"provider_duration_ms":1,"page_count":1,"estimated_cost":null,"cost_currency":null,"price_basis":null},
             "pages":[{"page_number":1,"canonical_text":"x","rotation_degrees":0}],
             "fields":[{"key":"x","occurrence":0,"value_type":"UndefinedSecret","value":"x","normalized_value":null,"confidence":1,"evidence":[]}],
             "warnings":[]}
            """;
        var client = CreateClient(body);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.ExtractAsync("safe/file.pdf", "application/pdf", CancellationToken.None));

        Assert.Equal("AI service returned an invalid field value type.", exception.Message);
        Assert.DoesNotContain("UndefinedSecret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Response_larger_than_five_mib_is_rejected_before_deserialization()
    {
        var content = new ProbeContent(5 * 1024 * 1024 + 1);
        var http = new HttpClient(new ContentHandler(content))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var client = new PythonExtractionClient(http);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.ExtractAsync("safe/file.pdf", "application/pdf", CancellationToken.None));

        Assert.False(content.ReadAttempted);
    }

    [Fact]
    public async Task Unknown_length_response_is_stopped_by_bounded_stream_after_five_mib()
    {
        var content = new UnknownLengthContent(5 * 1024 * 1024 + 1);
        Assert.Null(content.Headers.ContentLength);
        var http = new HttpClient(new ContentHandler(content))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var client = new PythonExtractionClient(http);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.ExtractAsync("safe/file.pdf", "application/pdf", CancellationToken.None));

        Assert.Equal("AI service response exceeds the normalized-result limit.", exception.Message);
        Assert.True(content.ReadAttempted);
    }

    private static PythonExtractionClient CreateClient(string body)
    {
        var http = new HttpClient(new StubHandler(body)) { BaseAddress = new Uri("http://localhost") };
        return new PythonExtractionClient(http);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }

    private sealed class ContentHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        });
    }

    private sealed class ProbeContent : HttpContent
    {
        private readonly long _length;

        public ProbeContent(long length)
        {
            _length = length;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Headers.ContentLength = length;
        }

        public bool ReadAttempted { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            throw new InvalidOperationException("Oversized content must not be read.");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _payload;

        public UnknownLengthContent(int stringBytes)
        {
            _payload = Encoding.UTF8.GetBytes($"\"{new string('x', stringBytes)}\"");
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        public bool ReadAttempted { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ReadAttempted = true;
            return stream.WriteAsync(_payload).AsTask();
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            ReadAttempted = true;
            return Task.FromResult<Stream>(new MemoryStream(_payload, writable: false));
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

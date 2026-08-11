using System.Net.Http.Json;
using System.Text.Json.Serialization;
using UaeDocumentIntelligence.Application.Abstractions;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class PythonExtractionClient(HttpClient client) : IExtractionClient
{
    public string ProviderName => "mock-invoice";
    public string ProviderVersion => "v1";

    public async Task<ExtractionResult> ExtractAsync(string absolutePath, string contentType,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/extract", new AiRequest(absolutePath, contentType), cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AiResponse>(cancellationToken)
            ?? throw new InvalidOperationException("AI service returned no result.");
        return new ExtractionResult(result.DocumentType, result.Language, result.Extractor,
            result.Fields.Select(x => new ExtractionField(x.Name, x.Value, x.Confidence, x.Page)).ToList());
    }

    private sealed record AiRequest(string FilePath, string ContentType);
    private sealed record AiField(string Name, string Value, decimal Confidence, int Page);
    private sealed record AiResponse(
        [property: JsonPropertyName("document_type")] string DocumentType,
        string Language,
        string Extractor,
        List<AiField> Fields);
}

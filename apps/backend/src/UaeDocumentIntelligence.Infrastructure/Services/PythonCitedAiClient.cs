using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Infrastructure.Services;

internal sealed class PythonCitedAiClient(HttpClient client, IOptions<AiServiceOptions> options,
    ILogger<PythonCitedAiClient>? logger = null, CitedGenerationSettings? generation = null) : ICitedAiClient, ICitedAnswerClient
{
    private const int Limit = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly JsonSerializerOptions AnswerJson = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 12 };

    public Task<ChunkingResult> ChunkAsync(ChunkingSnapshot snapshot, CancellationToken ct) =>
        PostAsync<ChunkingResult>("/internal/cited/chunks", snapshot, ct);
    public Task<LexicalPlan> PlanAsync(string question, int topK, CancellationToken ct) =>
        PostAsync<LexicalPlan>("/internal/cited/lexical-plan", new { question, topK }, ct);

    public Task<PrivateAnswerResult> ProposeAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct) =>
        PostBytesAsync<PrivateAnswerResult>("/internal/cited/answer-proposal", requestUtf8.ToArray(), 98304,
            generation?.OpenAiSyntheticPilot == true ? 524288 : 131072, ct,
            generation?.OpenAiSyntheticPilot == true ? 50 : 30);

    public Task<PrivateAnswerPreparation> PrepareAsync(ReadOnlyMemory<byte> requestUtf8, CancellationToken ct) =>
        PostBytesAsync<PrivateAnswerPreparation>("/internal/cited/answer-prepare", requestUtf8.ToArray(), 65536, 4096, ct);

    private Task<T> PostAsync<T>(string path, object payload, CancellationToken ct) =>
        PostBytesAsync<T>(path, JsonSerializer.SerializeToUtf8Bytes(payload, Json), Limit, Limit, ct);

    private async Task<T> PostBytesAsync<T>(string path, byte[] bytes, int requestLimit, int responseLimit, CancellationToken ct,
        int deadlineSeconds = 30)
    {
        if (bytes.Length > requestLimit)
            throw new CitedWorkflowException("index_input_too_large", "The indexing request exceeds the transport limit.", 413);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(deadlineSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new("application/json");
        if (!PrivateServiceCredentials.TryApply(request, client, options.Value))
            throw new CitedWorkflowException("private_service_unconfigured",
                "The private service credential or transport is not configured.", 503);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        int? status = null;
        var outcome = "failed";
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                throw new CitedWorkflowException("private_service_rejected", "The private cited service rejected the request.",
                    response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity ? 422 : 502);
            if (response.Content.Headers.ContentLength > responseLimit) throw InvalidResult();
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new MemoryStream();
            var block = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(block, deadline.Token)) != 0)
            {
                if (buffer.Length + read > responseLimit) throw InvalidResult();
                buffer.Write(block, 0, read);
            }
            T result;
            if (typeof(T) == typeof(PrivateAnswerResult) || typeof(T) == typeof(PrivateAnswerPreparation))
            {
                using var document = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), new JsonDocumentOptions { MaxDepth = 12 });
                RejectDuplicateKeys(document.RootElement);
                result = document.RootElement.Deserialize<T>(AnswerJson) ?? throw InvalidResult();
            }
            else result = JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), Json) ?? throw InvalidResult();
            outcome = "succeeded";
            return result;
        }
        catch (JsonException) { throw InvalidResult(); }
        catch (HttpRequestException) { throw new CitedWorkflowException("private_service_unavailable", "The private cited service is unavailable.", 503); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new CitedWorkflowException("private_service_timeout", "The private cited service timed out.", 504); }
        finally
        {
            // 'path' comes only from the fixed internal operations above, never
            // from source data or a caller URL. Do not attach exceptions or headers.
            logger?.LogInformation(new EventId(5110, "PrivateCitedOperationCompleted"),
                "Private cited operation completed: {Operation}; outcome {Outcome}; status {Status}; duration {DurationMs} ms",
                path, outcome, status, (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
    private static void RejectDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                RejectDuplicateKeys(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicateKeys(child);
    }
    private static CitedWorkflowException InvalidResult() =>
        new("invalid_private_result", "The private cited result was invalid or too large.", 502);
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Tests.Fixtures;

// Synthetic provider responses only. Hash fields describing tokenization are test receipts,
// not evidence that a tokenizer/provider was contacted.
internal sealed class EmbeddingFixture
{
    public Guid Operation { get; } = Guid.NewGuid();
    public JsonObject Dispatch { get; }
    public JsonObject Outcome { get; }
    public JsonObject Raw { get; }
    public int Tokens { get; }
    public byte[] PreparationBytes => Encoding.UTF8.GetBytes(Dispatch.ToJsonString());
    public byte[] OutcomeBytes => Encoding.UTF8.GetBytes(Outcome.ToJsonString());
    public byte[] RawBytes => Encoding.UTF8.GetBytes(Raw.ToJsonString());
    public decimal Cost => decimal.Ceiling(Tokens * .02m) / 1_000_000m;

    public EmbeddingFixture(Guid org, Guid actor, Guid[] indexes, (Guid Id, string Text)[] inputs, string purpose = "chunk", Guid? operation = null)
    {
        if (operation.HasValue) Operation = operation.Value;
        Tokens = inputs.Length * 10;
        var model = new { providerAlias = "openai", model = CitedEmbeddingStorage.Model,
            version = CitedEmbeddingStorage.Model, dimension = 1536, distance = "cosine" };
        var source = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = CitedEmbeddingStorage.Schema,
            context = new { organizationId = org, actorId = actor, operationId = Operation, purpose,
                indexIds = indexes.OrderBy(x => x.ToString(), StringComparer.Ordinal) }, model,
            inputs = inputs.Select(x => new { inputId = x.Id, text = x.Text,
                inputSha256 = CitedEmbeddingStorage.Digest(Encoding.UTF8.GetBytes(x.Text)) })
        });
        var sourceHash = CitedEmbeddingStorage.Digest(source);
        var bodyHash = CitedEmbeddingStorage.Digest("test-only-token-wire"u8.ToArray());
        var bindingHash = CitedEmbeddingStorage.Digest("test-only-binding"u8.ToArray());
        Dispatch = JsonSerializer.SerializeToNode(new
        {
            requestUtf8 = Convert.ToBase64String(source),
            preparation = new { schemaVersion = CitedEmbeddingStorage.Schema, sourceSha256 = sourceHash, model,
                bodySha256 = bodyHash, bindingSha256 = bindingHash, wireVersion = CitedEmbeddingStorage.Wire,
                counterVersion = CitedEmbeddingStorage.Counter, vocabularySha256 = CitedEmbeddingStorage.Vocabulary,
                inputTokens = Tokens, tokenCounts = inputs.Select(_ => 10).ToArray(), maximumOutputTokens = 0,
                maximumResponseBytes = 262144 }
        })!.AsObject();
        Raw = JsonSerializer.SerializeToNode(new
        {
            @object = "list", model = CitedEmbeddingStorage.Model,
            // Reverse order to verify explicit provider indices, rather than array order, bind each vector.
            data = inputs.Select((_, i) => new { @object = "embedding", index = i,
                embedding = Enumerable.Range(0, 1536).Select(n => n == i ? 1f : 0f).ToArray() }).Reverse(),
            usage = new { prompt_tokens = Tokens, total_tokens = Tokens }
        })!.AsObject();
        Outcome = JsonSerializer.SerializeToNode(new
        {
            schemaVersion = CitedEmbeddingStorage.Schema, sourceSha256 = sourceHash,
            bodySha256 = bodyHash, bindingSha256 = bindingHash, model, counterVersion = CitedEmbeddingStorage.Counter,
            wireVersion = CitedEmbeddingStorage.Wire, measuredInputTokens = Tokens, inputTokens = Tokens,
            outputTokens = 0, accountingKnown = true, outcome = "vectors", requiresReconciliation = false,
            providerRequestId = "req_test_embedding", httpStatus = 200, responseSha256 = "", responseUtf8 = "",
            publicationAllowed = false
        })!.AsObject();
        SyncRaw();
    }

    public void SyncRaw()
    {
        Outcome["responseSha256"] = CitedEmbeddingStorage.Digest(RawBytes);
        Outcome["responseUtf8"] = Convert.ToBase64String(RawBytes);
    }
}

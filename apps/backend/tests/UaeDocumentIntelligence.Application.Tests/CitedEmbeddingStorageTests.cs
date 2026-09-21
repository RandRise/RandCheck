using System.Text;
using System.Text.Json.Nodes;
using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Tests.Fixtures;

namespace UaeDocumentIntelligence.Application.Tests;

public class CitedEmbeddingStorageTests
{
    [Fact]
    public void Accepts_actual_Python_wire_and_local_tokenizer_fixture()
    {
        var f = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/embedding-python-v1.json")))!;
        var accepted = CitedEmbeddingStorage.Inspect(Encoding.UTF8.GetBytes(f["dispatch"]!.ToJsonString()),
            Encoding.UTF8.GetBytes(f["outcome"]!.ToJsonString()));
        Assert.Contains("شركة", accepted.Inputs[0].Text);
        Assert.Equal(f["dispatch"]!["preparation"]!["inputTokens"]!.GetValue<int>(), accepted.InputTokens);
        Assert.Equal(1f, accepted.Vectors[0][0]);
    }

    private static EmbeddingFixture Fixture() => new(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()],
        [(Guid.NewGuid(), "Synthetic invoice total 100.00"), (Guid.NewGuid(), "Supplier TRN 100000000000003")]);

    [Fact]
    public void Raw_provider_indices_bind_vectors_to_their_correct_sources()
    {
        var f = Fixture();
        var result = CitedEmbeddingStorage.Inspect(f.PreparationBytes, f.OutcomeBytes);
        Assert.Equal(1f, result.Vectors[0][0]);
        Assert.Equal(0f, result.Vectors[0][1]);
        Assert.Equal(1f, result.Vectors[1][1]);
        Assert.DoesNotContain("invoice", result.ToString());
        Assert.DoesNotContain("invoice", result.Inputs[0].ToString());
    }

    [Theory]
    [InlineData("raw_usage")]
    [InlineData("duplicate_index")]
    [InlineData("zero")]
    [InlineData("dimension")]
    [InlineData("overflow")]
    [InlineData("unknown_charge")]
    [InlineData("model")]
    [InlineData("underflow")]
    public void Known_settlement_does_not_make_invalid_vectors_reusable(string mutation)
    {
        var f = Fixture();
        switch (mutation)
        {
            case "raw_usage": f.Raw["usage"]!["prompt_tokens"] = 1; break;
            case "duplicate_index": f.Raw["data"]![0]!["index"] = 0; break;
            case "zero": f.Raw["data"]![0]!["embedding"]![1] = 0; break;
            case "dimension": f.Raw["data"]![0]!["embedding"]!.AsArray().RemoveAt(2); break;
            case "overflow": f.Raw["data"]![0]!["embedding"]![1] = 1.1; break;
            case "underflow": f.Raw["data"]![0]!["embedding"]![1] = 1e-100; break;
            case "unknown_charge": f.Raw["usage"]!["extra_charge"] = 1; break;
            case "model": f.Raw["model"] = "different-model"; break;
        }
        f.SyncRaw();
        Denied(f.PreparationBytes, f.OutcomeBytes);
    }

    [Theory]
    [InlineData("source_hash")]
    [InlineData("binding")]
    [InlineData("counter")]
    [InlineData("receipt_counts")]
    [InlineData("unknown")]
    [InlineData("publication")]
    [InlineData("reconcile")]
    [InlineData("raw_hash")]
    [InlineData("bad_base64")]
    public void Rejects_unbound_or_uncertain_saved_outcomes(string mutation)
    {
        var f = Fixture();
        switch (mutation)
        {
            case "source_hash": f.Outcome["sourceSha256"] = new string('0',64); break;
            case "binding": f.Outcome["bindingSha256"] = new string('0',64); break;
            case "counter": f.Dispatch["preparation"]!["counterVersion"] = "other-counter"; break;
            case "receipt_counts": f.Dispatch["preparation"]!["tokenCounts"]![0] = 1; break;
            case "unknown": f.Outcome["accountingKnown"] = false; f.Outcome["inputTokens"] = null; break;
            case "publication": f.Outcome["publicationAllowed"] = true; break;
            case "reconcile": f.Outcome["requiresReconciliation"] = true; break;
            case "raw_hash": f.Outcome["responseSha256"] = new string('0',64); break;
            case "bad_base64": f.Outcome["responseUtf8"] = "not base64"; break;
        }
        Denied(f.PreparationBytes, f.OutcomeBytes);
    }

    [Fact]
    public void Rejects_duplicate_json_keys_and_malformed_source_without_echoing_content()
    {
        var f = Fixture();
        var json = Encoding.UTF8.GetString(f.OutcomeBytes);
        Denied(f.PreparationBytes, Encoding.UTF8.GetBytes(json.Replace("\"outputTokens\":0", "\"outputTokens\":0,\"outputTokens\":0")));
        f.Dispatch["requestUtf8"] = Convert.ToBase64String("{bad private source"u8.ToArray());
        Denied(f.PreparationBytes, f.OutcomeBytes);
    }

    private static void Denied(byte[] preparation, byte[] outcome)
    {
        var e = Assert.Throws<CitedWorkflowException>(() => CitedEmbeddingStorage.Inspect(preparation, outcome));
        Assert.Equal("embedding_outcome_not_reusable", e.Code);
        Assert.DoesNotContain("invoice", e.ToString());
    }
}

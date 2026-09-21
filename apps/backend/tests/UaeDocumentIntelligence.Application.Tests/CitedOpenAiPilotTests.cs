using System.Security.Cryptography;
using System.Text.Json;
using UaeDocumentIntelligence.Application.CitedQuestions;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed class CitedOpenAiPilotTests
{
    [Fact]
    public void Low_reasoning_requires_explicit_matching_receipt_and_provider_response()
    {
        var original = Preparation();
        var low = original with { OpenAi = original.OpenAi! with { ReasoningEffort = "low" } };
        Assert.False(CitedOpenAiPilot.ValidPreparation(low));
        Assert.True(CitedOpenAiPilot.ValidPreparation(low, true));
        Assert.False(CitedOpenAiPilot.ValidPreparation(original, true));
        Assert.Throws<CitedWorkflowException>(() => CitedOpenAiPilot.Inspect(Result(), low));
        var result = Result();
        var json = System.Text.Json.Nodes.JsonNode.Parse(result.OpenAi!.ResponseUtf8!)!;
        json["reasoning"] = new System.Text.Json.Nodes.JsonObject { ["effort"] = "low" };
        var bytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
        var changed = result with { OpenAi = result.OpenAi with { ResponseUtf8 = bytes,
            ResponseSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() } };
        Assert.Equal(.000112m, CitedOpenAiPilot.Inspect(changed, low).Cost);
    }

    private static readonly string Hash = new('a', 64);
    private static PrivateAnswerPreparation Preparation() => new()
    {
        SchemaVersion = "1.0", RequestSha256 = Hash, Model = new("openai", "gpt-5-nano", CitedOpenAiPilot.Rate.ModelVersion),
        PromptSha256 = Hash, PromptVersion = "cited-answer-prompt-v1", CounterVersion = CitedOpenAiPilot.CounterVersion,
        InputTokens = 8000, MaximumOutputTokens = 4000, OpenAi = new(CitedOpenAiPilot.ProtocolVersion, Hash, Hash)
    };
    private static PrivateAnswerResult Result(int inputs = 1000, int outputs = 200, int cached = 400)
    {
        var response = JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = "resp_fixture", @object = "response", model = CitedOpenAiPilot.Rate.ModelVersion,
            status = "completed", service_tier = "default", tools = Array.Empty<object>(),
            usage = new { input_tokens = inputs, output_tokens = outputs, total_tokens = (long)inputs + outputs,
                input_tokens_details = new { cached_tokens = cached }, output_tokens_details = new { reasoning_tokens = 120 } }
        });
        return new()
        {
            SchemaVersion = "1.0", RequestSha256 = Hash, PromptSha256 = Hash, Model = Preparation().Model,
            Outcome = "completed", ProposalUtf8 = "{}"u8.ToArray(), InputTokens = inputs, OutputTokens = outputs,
            OpenAi = new() { GenerationSha256 = Hash, AccountingKnown = true, CountRequestId = "req_count",
                CountResponseUtf8 = "{\"object\":\"response.input_tokens\",\"input_tokens\":1000}"u8.ToArray(), MeasuredInputTokens = 1000,
                ProviderRequestId = "req_generation", ResponseId = "resp_fixture", ResponseUtf8 = response,
                ResponseSha256 = Convert.ToHexString(SHA256.HashData(response)).ToLowerInvariant(),
                ProviderStatus = "completed", CachedInputTokens = cached, ReasoningTokens = 120 }
        };
    }
    [Fact]
    public void Ceiling_is_explicit_and_not_an_actual_count()
    {
        Assert.True(CitedOpenAiPilot.ValidPreparation(Preparation()));
        Assert.False(CitedOpenAiPilot.ValidPreparation(Preparation() with { InputTokens = 1000 }));
        Assert.False(CitedOpenAiPilot.ValidPreparation(Preparation() with { OpenAi = null }));
    }
    [Fact]
    public void Cached_and_reasoning_usage_are_not_double_charged()
    {
        Assert.Equal(.000112m, CitedOpenAiPilot.Inspect(Result(), Preparation()).Cost);
        Assert.False(CitedOpenAiPilot.Inspect(Result(), Preparation()).Freeze);
    }
    [Fact]
    public void Overrun_is_charged_and_frozen_not_clamped()
    {
        var outcome = CitedOpenAiPilot.Inspect(Result(1001), Preparation());
        Assert.Equal(.000113m, outcome.Cost);
        Assert.True(outcome.Freeze);
        Assert.True(CitedOpenAiPilot.Inspect(Result(outputs: 4001), Preparation()).Freeze);
    }
    [Fact]
    public void Unknown_usage_requires_a_hold_instead_of_zero_cost()
    {
        var result = Result();
        var outcome = CitedOpenAiPilot.Inspect(result with { OpenAi = result.OpenAi! with { AccountingKnown = false } }, Preparation());
        Assert.Null(outcome.Cost);
        Assert.Null(outcome.Evidence);
        Assert.True(outcome.Freeze);
    }
    [Theory]
    [InlineData("hash")]
    [InlineData("wire")]
    [InlineData("tokens")]
    [InlineData("count")]
    [InlineData("request")]
    [InlineData("cached")]
    public void Inconsistent_or_unbound_accounting_is_rejected(string mutation)
    {
        var result = Result();
        result = mutation switch
        {
            "hash" => result with { OpenAi = result.OpenAi! with { ResponseSha256 = new string('b', 64) } },
            "wire" => result with { OpenAi = result.OpenAi! with { GenerationSha256 = new string('b', 64) } },
            "tokens" => result with { InputTokens = 101 },
            "count" => result with { OpenAi = result.OpenAi! with { MeasuredInputTokens = 999 } },
            "request" => result with { OpenAi = result.OpenAi! with { ProviderRequestId = "SECRET\nKEY" } },
            _ => result with { OpenAi = result.OpenAi! with { CachedInputTokens = 1001 } }
        };
        var error = Assert.Throws<CitedWorkflowException>(() => CitedOpenAiPilot.Inspect(result, Preparation()));
        Assert.DoesNotContain("SECRET", error.ToString());
    }
}

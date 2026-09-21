using UaeDocumentIntelligence.Application.CitedQuestions;
using UaeDocumentIntelligence.Tests.Fixtures;

namespace UaeDocumentIntelligence.Application.Tests;

public class CitedEmbeddingAccountingTests
{
    private static readonly CitedGenerationRate Rate = new("openai", CitedEmbeddingStorage.Model, CitedEmbeddingStorage.Model,
        "test-rate", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), .02m, 0m);
    private static EmbeddingFixture Fixture() => new(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], [(Guid.NewGuid(), "Invoice total 100.00")]);

    [Fact]
    public void Invalid_vectors_preserve_independently_checked_billable_usage()
    {
        var f = Fixture();
        f.Raw["data"]!.AsArray().Clear();
        f.Outcome["outcome"] = "invalid_vectors";
        f.SyncRaw();
        var result = CitedEmbeddingProtocol.Accounting(f.PreparationBytes, f.OutcomeBytes, Rate);
        Assert.Equal(f.Cost, result.Cost);
        Assert.False(result.RequiresReconciliation);
        Assert.Throws<CitedWorkflowException>(() => CitedEmbeddingStorage.Inspect(f.PreparationBytes, f.OutcomeBytes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10001)]
    public void Usage_discrepancy_preserves_raw_cost_and_freezes_admission(int tokens)
    {
        var f = Fixture();
        f.Raw["usage"]!["prompt_tokens"] = tokens;
        f.Raw["usage"]!["total_tokens"] = tokens;
        f.SyncRaw();
        var result = CitedEmbeddingProtocol.Accounting(f.PreparationBytes, f.OutcomeBytes, Rate);
        Assert.Equal(decimal.Ceiling(tokens * .02m) / 1000000m, result.Cost);
        Assert.True(result.RequiresReconciliation);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("binding")]
    [InlineData("usage")]
    [InlineData("model")]
    [InlineData("extra_charge")]
    [InlineData("missing")]
    public void Unbound_or_unknown_accounting_is_never_free(string mode)
    {
        var f = Fixture();
        if (mode == "hash") f.Outcome["responseSha256"] = "bad";
        if (mode == "binding") f.Outcome["bindingSha256"] = new string('0',64);
        if (mode == "usage") { f.Raw.Remove("usage"); f.SyncRaw(); }
        if (mode == "model") { f.Raw["model"] = "other"; f.SyncRaw(); }
        if (mode == "extra_charge") { f.Raw["usage"]!["extra_fee"] = 1; f.SyncRaw(); }
        if (mode == "missing") f.Outcome["responseUtf8"] = null;
        var result = CitedEmbeddingProtocol.Accounting(f.PreparationBytes, f.OutcomeBytes, Rate);
        Assert.Null(result.Cost);
        Assert.True(result.RequiresReconciliation);
    }
}

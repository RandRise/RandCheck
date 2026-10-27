using UaeDocumentIntelligence.Application.Reconciliation;

namespace UaeDocumentIntelligence.Application.Tests;

public sealed partial class ReconciliationHandlerTests
{
    [Fact]
    public async Task Results_v2_keeps_differences_and_missing_checks_visible_together()
    {
        var context = PairHandlerFixture(raw: "1261.00", genericTrn: true);
        var response = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken,
            new(RuleSetVersion: "invoice-po-2.0"), default);
        var coverage = Assert.Single(response.Runs).Coverage!;
        Assert.Equal("DifferencesFound", coverage.Outcome);
        Assert.Equal("Incomplete", coverage.Status);
        Assert.True(coverage.Differences > 0);
        Assert.True(coverage.CouldNotCheck > 0);
        Assert.Equal(coverage.TotalChecks, coverage.PassedChecks + coverage.Differences + coverage.CouldNotCheck);
    }

    [Theory]
    [InlineData("invoice-po-1.0", "1260.00", false, "AllRequiredChecksPassed")]
    [InlineData("invoice-po-2.0", "1260.00", false, "AllRequiredChecksPassed")]
    [InlineData("invoice-po-2.0", "1261.00", false, "DifferencesFound")]
    [InlineData("invoice-po-2.0", "1260.00", true, "ChecksIncomplete")]
    public async Task Results_coverage_counts_saved_checks_without_overlap(string policy, string total, bool uncheckedInvoice, string outcome)
    {
        var context = PairHandlerFixture(raw: total, uncheckedInvoice: uncheckedInvoice);
        var response = await context.Handler.HandleAsync(context.Case.Id, context.Case.ConcurrencyToken,
            new(RuleSetVersion: policy, OrderScope: policy == "invoice-po-1.0" ? "FullOrder" : null), default);
        var run = Assert.Single(response.Runs);
        var checks = run.Checks!;
        var coverage = run.Coverage!;
        Assert.Equal(outcome, coverage.Outcome);
        Assert.Equal(checks.Count, coverage.TotalChecks);
        Assert.Equal(checks.Count(check => check.Status == "NoDifference"), coverage.PassedChecks);
        Assert.Equal(checks.Count(check => check.Status == "DifferenceFound"), coverage.Differences);
        Assert.Equal(checks.Count(check => check.Status == "CouldNotCheck"), coverage.CouldNotCheck);
        Assert.Equal(coverage.PassedChecks + coverage.Differences, coverage.CompletedChecks);
        Assert.Equal(checks.Count, coverage.CompletedChecks + coverage.CouldNotCheck);
        Assert.All(run.Sources!, source => Assert.Contains(context.Case.DocumentLinks,
            link => link.DocumentId == source.DocumentId && link.ReviewSessionId == source.ReviewSessionId && link.ExtractionRunId == source.ExtractionRunId));
    }
}

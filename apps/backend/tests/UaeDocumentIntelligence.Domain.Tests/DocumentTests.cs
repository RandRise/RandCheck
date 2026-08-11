using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class DocumentTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_file_size(long sizeBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDocument(sizeBytes: sizeBytes));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeg")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
    public void Constructor_rejects_non_canonical_sha256(string sha256)
    {
        Assert.Throws<ArgumentException>(() => CreateDocument(sha256: sha256));
    }

    [Fact]
    public void Constructor_sets_stored_state_and_fresh_concurrency_token()
    {
        var document = CreateDocument();

        Assert.Equal(DocumentProcessingStatus.Stored, document.ProcessingStatus);
        Assert.Equal(Now, document.CreatedAtUtc);
        Assert.Equal(Now, document.UpdatedAtUtc);
        Assert.NotEqual(Guid.Empty, document.ConcurrencyToken);
    }

    [Fact]
    public void Failed_retry_preserves_prior_success_and_selected_output()
    {
        var document = CreateDocument();
        var firstRun = document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request-1", Now);
        firstRun.Start(Now.AddSeconds(1));
        firstRun.Succeed("invoice", "en", Now.AddSeconds(2));
        document.RecordExtractionSuccess(firstRun, Now.AddSeconds(2));

        var tokenAfterSuccess = document.ConcurrencyToken;
        var retry = document.QueueExtraction(Guid.NewGuid(), 2, "mock", "2", "invoice-v1", "request-2", Now.AddSeconds(3));
        retry.Start(Now.AddSeconds(4));
        retry.Fail("provider_failure", "Provider was unavailable.", Now.AddSeconds(5));
        document.RecordExtractionFailure(retry, Now.AddSeconds(5));

        Assert.Equal(DocumentProcessingStatus.ReadyForReview, document.ProcessingStatus);
        Assert.Same(firstRun, document.SelectedExtractionRun());
        Assert.NotEqual(tokenAfterSuccess, document.ConcurrencyToken);
        Assert.Equal(2, document.ExtractionRuns.Count);
    }

    [Fact]
    public void Latest_successful_retry_becomes_selected_output()
    {
        var document = CreateDocument();
        var first = CompleteSuccessfulRun(document, 1, Now);
        var second = CompleteSuccessfulRun(document, 2, Now.AddMinutes(1));

        Assert.NotSame(first, second);
        Assert.Same(second, document.SelectedExtractionRun());
    }

    [Fact]
    public void Duplicate_run_number_is_rejected_without_mutating_history()
    {
        var document = CreateDocument();
        document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request-1", Now);

        Assert.Throws<InvalidOperationException>(() =>
            document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request-2", Now));
        Assert.Single(document.ExtractionRuns);
    }

    [Fact]
    public void Document_rejects_terminal_result_from_unowned_run()
    {
        var document = CreateDocument();
        var otherDocument = CreateDocument();
        var run = otherDocument.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request", Now);
        run.Start(Now);
        run.Succeed("invoice", "en", Now.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => document.RecordExtractionSuccess(run, Now.AddSeconds(1)));
    }

    private static ExtractionRun CompleteSuccessfulRun(Document document, int runNumber, DateTimeOffset now)
    {
        var run = document.QueueExtraction(Guid.NewGuid(), runNumber, "mock", "1", "invoice-v1", $"request-{runNumber}", now);
        run.Start(now.AddSeconds(1));
        run.Succeed("invoice", "en", now.AddSeconds(2));
        document.RecordExtractionSuccess(run, now.AddSeconds(2));
        return run;
    }

    private static Document CreateDocument(long sizeBytes = 100, string sha256 = ValidSha256) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf", "opaque-key.pdf",
            "application/pdf", sizeBytes, sha256, Now);
}

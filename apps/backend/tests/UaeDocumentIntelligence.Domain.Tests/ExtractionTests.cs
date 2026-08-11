using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class ExtractionTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Run_follows_queued_running_succeeded_transition()
    {
        var run = CreateRun();

        Assert.Equal(ExtractionRunStatus.Queued, run.Status);
        run.Start(Now.AddSeconds(1));
        run.Succeed("invoice", "en", Now.AddSeconds(3));

        Assert.Equal(ExtractionRunStatus.Succeeded, run.Status);
        Assert.Equal(Now.AddSeconds(1), run.StartedAtUtc);
        Assert.Equal(Now.AddSeconds(3), run.CompletedAtUtc);
        Assert.Equal(2000, run.DurationMs);
    }

    [Fact]
    public void Run_rejects_invalid_state_transitions_and_field_addition_outside_running_state()
    {
        var run = CreateRun();

        Assert.Throws<InvalidOperationException>(() => AddField(run, 0.5m));
        Assert.Throws<InvalidOperationException>(() => run.Succeed("invoice", "en", Now));

        run.Start(Now);
        Assert.Throws<InvalidOperationException>(() => run.Start(Now));
        run.Fail("failed", "Safe failure.", Now.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => AddField(run, 0.5m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void Field_accepts_confidence_boundaries(string value)
    {
        var run = CreateRunningRun();
        var confidence = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        var field = AddField(run, confidence);

        Assert.Equal(confidence, field.Confidence);
    }

    [Theory]
    [InlineData("-0.0001")]
    [InlineData("1.0001")]
    public void Field_rejects_confidence_outside_unit_interval(string value)
    {
        var run = CreateRunningRun();
        var confidence = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => AddField(run, confidence));
    }

    [Fact]
    public void Duplicate_field_occurrence_is_rejected()
    {
        var run = CreateRunningRun();
        AddField(run, 0.5m);

        Assert.Throws<InvalidOperationException>(() => AddField(run, 0.6m));
        Assert.Single(run.Fields);
    }

    [Fact]
    public void Field_supports_multiple_page_evidence_records()
    {
        var run = CreateRunningRun();
        var field = AddField(run, 0.9m);

        var first = run.AddEvidence(field, Guid.NewGuid(), 1, "Total: AED 100", 0, 14);
        var second = run.AddEvidence(field, Guid.NewGuid(), 2,
            boundingPolygon: "0,0,1,0,1,1,0,1", coordinateUnit: "normalized");

        Assert.Equal(2, field.Evidence.Count);
        Assert.Equal(1, first.PageNumber);
        Assert.Equal(2, second.PageNumber);
    }

    [Fact]
    public void Evidence_rejects_invalid_page_offsets_and_polygon_metadata()
    {
        var run = CreateRunningRun();
        var field = AddField(run, 0.9m);

        Assert.Throws<ArgumentOutOfRangeException>(() => run.AddEvidence(field, Guid.NewGuid(), 0));
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1, characterStart: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            characterStart: 5, characterEnd: 4));
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "polygon"));
        Assert.Empty(field.Evidence);
    }

    [Fact]
    public void Terminal_run_cannot_accept_new_evidence()
    {
        var run = CreateRunningRun();
        var field = AddField(run, 0.9m);
        run.Succeed("invoice", "en", Now.AddSeconds(1));

        Assert.Throws<InvalidOperationException>(() => run.AddEvidence(field, Guid.NewGuid(), 1));
        Assert.Empty(field.Evidence);
    }

    private static ExtractionRun CreateRun()
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            "opaque-key.pdf", "application/pdf", 100, ValidSha256, Now);
        return document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request-1", Now);
    }

    private static ExtractionRun CreateRunningRun()
    {
        var run = CreateRun();
        run.Start(Now);
        return run;
    }

    private static MachineFieldValue AddField(ExtractionRun run, decimal confidence) =>
        run.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, "100.00", confidence, Now);
}

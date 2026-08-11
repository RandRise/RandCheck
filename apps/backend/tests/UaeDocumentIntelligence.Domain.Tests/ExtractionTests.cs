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
        AddReviewableResult(run);
        run.Succeed("invoice", "en", Now.AddSeconds(3), "mock-model", "v1");

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
        Assert.Throws<InvalidOperationException>(() =>
            run.Succeed("invoice", "en", Now, "mock-model", "v1"));

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

    [Fact]
    public void Field_accepts_unknown_confidence()
    {
        var run = CreateRunningRun();

        var field = AddField(run, null);

        Assert.Null(field.Confidence);
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
        run.AddPage(Guid.NewGuid(), 1, "Total: AED 100", 0);
        run.AddPage(Guid.NewGuid(), 2, "Second page", 90);
        var field = AddField(run, 0.9m);

        var first = run.AddEvidence(field, Guid.NewGuid(), 1, "Total: AED 100", 0, 14);
        var second = run.AddEvidence(field, Guid.NewGuid(), 2,
            boundingPolygon: "[[0,0],[1,0],[1,1],[0,1]]", coordinateUnit: "normalized");

        Assert.Equal(2, field.Evidence.Count);
        Assert.Equal(1, first.PageNumber);
        Assert.Equal(2, second.PageNumber);
    }

    [Fact]
    public void Evidence_rejects_invalid_page_offsets_and_polygon_metadata()
    {
        var run = CreateRunningRun();
        run.AddPage(Guid.NewGuid(), 1, "Total: AED 100", 0);
        var field = AddField(run, 0.9m);

        Assert.Throws<InvalidOperationException>(() => run.AddEvidence(field, Guid.NewGuid(), 0));
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1, byteStart: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            quotedText: "", byteStart: 5, byteEnd: 4));
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "polygon"));
        Assert.Empty(field.Evidence);
    }

    [Fact]
    public void Terminal_run_cannot_accept_new_evidence()
    {
        var run = CreateRunningRun();
        var field = AddReviewableResult(run);
        run.Succeed("invoice", "en", Now.AddSeconds(1), "mock-model", "v1");

        Assert.Throws<InvalidOperationException>(() => run.AddEvidence(field, Guid.NewGuid(), 1));
        Assert.Single(field.Evidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void Page_accepts_canonical_text_and_supported_rotations(short rotation)
    {
        var run = CreateRunningRun();

        var page = run.AddPage(Guid.NewGuid(), 1, "Caf\u00e9\n  Total\t100", rotation);

        Assert.Equal("Caf\u00e9\n  Total\t100", page.CanonicalText);
        Assert.Equal(rotation, page.RotationDegrees);
    }

    [Theory]
    [InlineData(-90)]
    [InlineData(1)]
    [InlineData(360)]
    public void Page_rejects_unsupported_rotations(short rotation)
    {
        var run = CreateRunningRun();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            run.AddPage(Guid.NewGuid(), 1, "Invoice", rotation));
        Assert.Empty(run.Pages);
    }

    [Theory]
    [InlineData("Cafe\u0301")]
    [InlineData("Line one\r\nLine two")]
    [InlineData("Line one\rLine two")]
    public void Page_rejects_non_canonical_text(string text)
    {
        var run = CreateRunningRun();

        Assert.Throws<ArgumentException>(() => run.AddPage(Guid.NewGuid(), 1, text, 0));
        Assert.Empty(run.Pages);
    }

    [Fact]
    public void Pages_must_be_added_contiguously_and_once()
    {
        var run = CreateRunningRun();
        run.AddPage(Guid.NewGuid(), 1, "First", 0);

        Assert.Throws<InvalidOperationException>(() => run.AddPage(Guid.NewGuid(), 1, "Duplicate", 0));
        Assert.Throws<InvalidOperationException>(() => run.AddPage(Guid.NewGuid(), 3, "Gap", 0));
        Assert.Single(run.Pages);
    }

    [Fact]
    public void Evidence_uses_end_exclusive_utf8_byte_offsets_for_multibyte_text()
    {
        var run = CreateRunningRun();
        run.AddPage(Guid.NewGuid(), 1, "A\u00e9 \u0636", 0);
        var field = AddField(run, null);

        var evidence = run.AddEvidence(field, Guid.NewGuid(), 1, "\u00e9 \u0636", 1, 6);

        Assert.Equal(1, evidence.ByteStart);
        Assert.Equal(6, evidence.ByteEnd);
        Assert.Equal("\u00e9 \u0636", evidence.QuotedText);
        Assert.Throws<ArgumentException>(() =>
            run.AddEvidence(field, Guid.NewGuid(), 1, "\u00e9", 2, 3));
        Assert.Single(field.Evidence);
    }

    [Fact]
    public void Evidence_normalizes_valid_polygon_and_rejects_invalid_shapes()
    {
        var run = CreateRunningRun();
        run.AddPage(Guid.NewGuid(), 1, "Invoice", 270);
        var field = AddField(run, 0.9m);

        var evidence = run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "[[0.1,0.2],[0.8,0.2],[0.8,0.7],[0.1,0.7]]",
            coordinateUnit: "normalized");

        Assert.Equal("[[0.1,0.2],[0.8,0.2],[0.8,0.7],[0.1,0.7]]", evidence.BoundingPolygon);
        Assert.Equal("normalized", evidence.CoordinateUnit);
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "[[0,0],[1,0],[1,1]]", coordinateUnit: "normalized"));
        Assert.Throws<ArgumentOutOfRangeException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "[[0,0],[1.1,0],[1,1],[0,1]]", coordinateUnit: "normalized"));
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "[[0,0],[0,1],[1,1],[1,0]]", coordinateUnit: "normalized"));
        Assert.Throws<ArgumentException>(() => run.AddEvidence(field, Guid.NewGuid(), 1,
            boundingPolygon: "[[0,0],[1,0],[1,1],[0,1]]", coordinateUnit: "pixels"));
        Assert.Single(field.Evidence);
    }

    [Fact]
    public void Ready_and_partial_enforce_typed_warning_severity_rules()
    {
        var ready = CreateRunningRun();
        AddReviewableResult(ready);
        ready.AddWarning(Guid.NewGuid(), "layout_detected", ExtractionWarningSeverity.Info,
            "A supported layout was detected.", 1, "total", 0);
        ready.Succeed("invoice", "en", Now.AddSeconds(1), "mock-model", "v1");

        var readyWithDegradation = CreateRunningRun();
        AddReviewableResult(readyWithDegradation);
        readyWithDegradation.AddWarning(Guid.NewGuid(), "page_ocr_degraded",
            ExtractionWarningSeverity.Degraded, "One page produced incomplete OCR text.", 1);
        Assert.Throws<InvalidOperationException>(() =>
            readyWithDegradation.Succeed("invoice", "en", Now.AddSeconds(1), "mock-model", "v1"));

        var partialWithoutDegradation = CreateRunningRun();
        AddReviewableResult(partialWithoutDegradation);
        Assert.Throws<InvalidOperationException>(() =>
            partialWithoutDegradation.CompletePartial("invoice", "en", Now.AddSeconds(1), "mock-model", "v1"));

        var partial = CreateRunningRun();
        AddReviewableResult(partial);
        partial.AddWarning(Guid.NewGuid(), "page_ocr_degraded", ExtractionWarningSeverity.Degraded,
            "One page produced incomplete OCR text.", 1);
        partial.CompletePartial("invoice", "en", Now.AddSeconds(1), "mock-model", "v1");

        Assert.Equal(ExtractionRunStatus.Succeeded, ready.Status);
        Assert.Equal(ExtractionRunStatus.Partial, partial.Status);
        Assert.Single(partial.Warnings);
    }

    [Fact]
    public void Warnings_validate_scope_uniqueness_and_deterministic_order()
    {
        var run = CreateRunningRun();
        AddReviewableResult(run);

        var fieldWarning = run.AddWarning(Guid.NewGuid(), "z_field", ExtractionWarningSeverity.Info,
            "Field information.", 1, "total", 0);
        var runWarning = run.AddWarning(Guid.NewGuid(), "a_run", ExtractionWarningSeverity.Info,
            "Run information.");
        var degraded = run.AddWarning(Guid.NewGuid(), "page_degraded", ExtractionWarningSeverity.Degraded,
            "Page quality was degraded.", 1);

        Assert.Equal(new[] { runWarning, fieldWarning, degraded }, run.Warnings);
        Assert.Throws<InvalidOperationException>(() => run.AddWarning(Guid.NewGuid(), "z_field",
            ExtractionWarningSeverity.Info, "Duplicate.", 1, "total", 0));
        Assert.Throws<InvalidOperationException>(() => run.AddWarning(Guid.NewGuid(), "missing_page",
            ExtractionWarningSeverity.Info, "Missing page.", 2));
        Assert.Throws<InvalidOperationException>(() => run.AddWarning(Guid.NewGuid(), "missing_field",
            ExtractionWarningSeverity.Info, "Missing field.", 1, "subtotal", 0));
    }

    private static ExtractionRun CreateRun()
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            "opaque-key.pdf", "application/pdf", 100, ValidSha256, Now);
        return document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "2.0", "request-1", Now);
    }

    private static ExtractionRun CreateRunningRun()
    {
        var run = CreateRun();
        run.Start(Now);
        return run;
    }

    private static MachineFieldValue AddField(ExtractionRun run, decimal? confidence) =>
        run.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, "100.00", confidence, Now);

    private static MachineFieldValue AddReviewableResult(ExtractionRun run)
    {
        run.AddPage(Guid.NewGuid(), 1, "Total 100.00", 0);
        var field = AddField(run, 0.9m);
        run.AddEvidence(field, Guid.NewGuid(), 1, "100.00", 6, 12,
            "[[0.1,0.2],[0.8,0.2],[0.8,0.3],[0.1,0.3]]", "normalized");
        run.RecordProviderMetrics(25, 1, null, null, null);
        return field;
    }
}

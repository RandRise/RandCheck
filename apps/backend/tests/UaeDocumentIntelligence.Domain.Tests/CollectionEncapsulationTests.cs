using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class CollectionEncapsulationTests
{
    private const string ValidSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Aggregate_collections_cannot_be_mutated_through_public_interfaces()
    {
        var document = new Document(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf",
            "opaque.pdf", "application/pdf", 100, ValidSha256, Now);
        var run = document.QueueExtraction(Guid.NewGuid(), 1, "mock", "1", "invoice-v1", "request", Now);
        run.Start(Now);
        var page = run.AddPage(Guid.NewGuid(), 1, "Total 100.00", 0);
        var field = run.AddField(Guid.NewGuid(), "total", 0, FieldValueType.Decimal, "100.00", 0.9m, Now);
        var evidence = run.AddEvidence(field, Guid.NewGuid(), 1, "100.00", 6, 12);
        var warning = run.AddWarning(Guid.NewGuid(), "layout_detected", ExtractionWarningSeverity.Info,
            "A supported layout was detected.", 1, "total", 0);
        var session = new ReviewSession(Guid.NewGuid(), document.OrganizationId, document.Id, run.Id, 1, Now);
        session.Start(Now);
        var action = session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 1, ReviewActionType.Confirmed,
            "total", 0, field.Id, null, null, null, Now);

        AssertReadOnly(document.ExtractionRuns, run);
        AssertReadOnly(document.ReviewSessions, session, expectedCount: 0);
        AssertReadOnly(run.Pages, page);
        AssertReadOnly(run.Fields, field);
        AssertReadOnly(run.Warnings, warning);
        AssertReadOnly(field.Evidence, evidence);
        AssertReadOnly(session.Actions, action);
    }

    private static void AssertReadOnly<T>(IReadOnlyCollection<T> source, T attemptedItem,
        int? expectedCount = null)
    {
        var original = source.ToArray();
        var mutableView = Assert.IsAssignableFrom<ICollection<T>>(source);

        Assert.True(mutableView.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        Assert.Throws<NotSupportedException>(() => mutableView.Add(attemptedItem));
        Assert.Equal(expectedCount ?? original.Length, source.Count);
        Assert.Equal(original, source);
    }
}

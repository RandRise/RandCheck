using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class ReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    public static TheoryData<ReviewActionType, Guid?, FieldValueType?, string?, string?> ValidActions => new()
    {
        { ReviewActionType.Confirmed, Guid.NewGuid(), null, null, null },
        { ReviewActionType.Corrected, Guid.NewGuid(), FieldValueType.Decimal, "105.00", "Corrected against page 1." },
        { ReviewActionType.Added, null, FieldValueType.String, "AED", "Missing provider field." },
        { ReviewActionType.Cleared, Guid.NewGuid(), null, null, "Value does not appear in the document." },
        { ReviewActionType.Cleared, null, null, null, "Added field should be removed." }
    };

    public static TheoryData<ReviewActionType, Guid?, FieldValueType?, string?, string?> InvalidActions => new()
    {
        { ReviewActionType.Confirmed, null, null, null, null },
        { ReviewActionType.Confirmed, Guid.NewGuid(), FieldValueType.String, "changed", null },
        { ReviewActionType.Confirmed, Guid.NewGuid(), FieldValueType.String, null, null },
        { ReviewActionType.Confirmed, Guid.NewGuid(), null, "changed", null },
        { ReviewActionType.Corrected, null, FieldValueType.Decimal, "105.00", "reason" },
        { ReviewActionType.Corrected, Guid.NewGuid(), null, null, "reason" },
        { ReviewActionType.Corrected, Guid.NewGuid(), FieldValueType.Decimal, "105.00", null },
        { ReviewActionType.Added, Guid.NewGuid(), FieldValueType.String, "AED", "reason" },
        { ReviewActionType.Added, null, null, null, "reason" },
        { ReviewActionType.Added, null, FieldValueType.String, "AED", null },
        { ReviewActionType.Cleared, null, FieldValueType.String, "changed", "reason" },
        { ReviewActionType.Cleared, null, FieldValueType.String, null, "reason" },
        { ReviewActionType.Cleared, null, null, "changed", "reason" },
        { ReviewActionType.Cleared, null, null, null, null },
        { (ReviewActionType)999, Guid.NewGuid(), null, null, null },
        { ReviewActionType.Corrected, Guid.NewGuid(), (FieldValueType)999, "105.00", "reason" }
    };

    [Theory]
    [MemberData(nameof(ValidActions))]
    public void Review_action_matrix_accepts_valid_combinations(ReviewActionType action, Guid? machineFieldId,
        FieldValueType? correctedType, string? correctedValue, string? reason)
    {
        var session = CreateActiveSession();

        var item = session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 1, action, "total", 0,
            machineFieldId, correctedType, correctedValue, reason, Now);

        Assert.Equal(action, item.Action);
        Assert.Single(session.Actions);
    }

    [Theory]
    [MemberData(nameof(InvalidActions))]
    public void Review_action_matrix_rejects_invalid_combinations(ReviewActionType action, Guid? machineFieldId,
        FieldValueType? correctedType, string? correctedValue, string? reason)
    {
        var session = CreateActiveSession();

        Assert.Throws<ArgumentException>(() => session.AddAction(Guid.NewGuid(), Guid.NewGuid(), 1,
            action, "total", 0, machineFieldId, correctedType, correctedValue, reason, Now));
        Assert.Empty(session.Actions);
    }

    [Fact]
    public void Review_session_follows_pending_active_completed_transition()
    {
        var session = CreateSession();
        var initialToken = session.ConcurrencyToken;

        session.Start(Now.AddSeconds(1));
        var activeToken = session.ConcurrencyToken;
        session.Complete([], Now.AddSeconds(2));

        Assert.Equal(ReviewStatus.Completed, session.Status);
        Assert.Equal(Now.AddSeconds(1), session.StartedAtUtc);
        Assert.Equal(Now.AddSeconds(2), session.CompletedAtUtc);
        Assert.NotEqual(initialToken, activeToken);
        Assert.NotEqual(activeToken, session.ConcurrencyToken);
    }

    [Fact]
    public void Review_session_rejects_invalid_transitions_and_actions_outside_active_state()
    {
        var session = CreateSession();

        Assert.Throws<InvalidOperationException>(() => session.Complete([], Now));
        Assert.Throws<InvalidOperationException>(() => AddConfirmation(session, 1));
        session.Start(Now);
        session.Complete([], Now.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => session.Start(Now.AddSeconds(2)));
        Assert.Throws<InvalidOperationException>(() => AddConfirmation(session, 1));
    }

    [Fact]
    public void Duplicate_action_sequence_is_rejected_without_overwriting_history()
    {
        var session = CreateActiveSession();
        var first = AddConfirmation(session, 1);

        Assert.Throws<InvalidOperationException>(() => AddConfirmation(session, 1));
        Assert.Single(session.Actions);
        Assert.Same(first, session.Actions.Single());
    }

    [Fact]
    public void Review_session_requires_positive_revision()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewSession(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 0, Now));
    }

    private static ReviewSession CreateSession() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Now);

    private static ReviewSession CreateActiveSession()
    {
        var session = CreateSession();
        session.Start(Now);
        return session;
    }

    private static ReviewAction AddConfirmation(ReviewSession session, int sequenceNumber) =>
        session.AddAction(Guid.NewGuid(), Guid.NewGuid(), sequenceNumber, ReviewActionType.Confirmed,
            "total", 0, Guid.NewGuid(), null, null, null, Now);
}

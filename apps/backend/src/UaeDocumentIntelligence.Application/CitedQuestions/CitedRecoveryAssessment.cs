using UaeDocumentIntelligence.Application.Abstractions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

public sealed record CitedRecoverySnapshot(Guid ReservationId, Guid OrganizationId, Guid OriginalActorId,
    Guid OperationId, string RequestSha256, string Provider, string Model, string ModelVersion,
    CitedSpendState State, int EventSequence, decimal ReservedUsd, decimal? ActualUsd, DateTimeOffset CreatedAtUtc);

public enum CitedRecoveryEvidenceKind { FinalCost, ConfirmedNoCharge }

// A trusted verifier supplies this attestation. Constructing this record is NOT verification.
// Never bind it from an HTTP request or accept caller-supplied amounts/verification flags.
public sealed record CitedRecoveryEvidence(Guid EvidenceId, string Reference, Guid OrganizationId,
    Guid ReservationId, Guid OperationId, string RequestSha256, string Provider, string Model,
    string ModelVersion, CitedRecoveryEvidenceKind Kind, string Currency, decimal ActualUsd,
    bool IsFinal, DateTimeOffset VerifiedAtUtc, DateTimeOffset ValidUntilUtc);

public enum CitedRecoveryRecommendation { KeepHold, CancelReserved, RecordVerifiedCost, NoChange }

public sealed record CitedRecoveryAssessment(Guid ReservationId, Guid OrganizationId, Guid OperatorActorId,
    Guid OriginalActorId, CitedSpendState ExpectedState, int ExpectedEventSequence,
    CitedRecoveryRecommendation Recommendation, string ReasonCode, Guid? EvidenceId,
    decimal? VerifiedActualUsd, bool RequiresOverrunFreeze)
{
    public string PolicyVersion => "cited-recovery-assessment-v1";
    public bool Executable => false; // Preview only; no capability/token accepted by a writer.
    public bool MayClearSharedFreeze => false;
    public bool MayRedispatch => false;
}

/// <summary>Separate privilege from invoice access or the fixed development identity.</summary>
public interface ICitedRecoveryAuthorization
{
    Task<bool> CanAssessAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct);
}
public interface ICitedRecoveryReader
{
    // Check the active operator and organization; the original requester may be inactive.
    Task<CitedRecoverySnapshot?> ReadAsync(Guid organizationId, Guid operatorActorId, Guid reservationId, CancellationToken ct);
}
public interface ICitedRecoveryEvidenceVerifier
{
    // Opaque reference lookup in trusted evidence storage, not URL fetching or request-body cost.
    Task<CitedRecoveryEvidence?> VerifyAsync(CitedRecoverySnapshot snapshot, string reference, CancellationToken ct);
}

public sealed class DenyCitedRecoveryAuthorization : ICitedRecoveryAuthorization
{
    public Task<bool> CanAssessAsync(Guid organizationId, Guid operatorActorId, CancellationToken ct) => Task.FromResult(false);
}
public sealed class UnavailableCitedRecoveryEvidenceVerifier : ICitedRecoveryEvidenceVerifier
{
    public Task<CitedRecoveryEvidence?> VerifyAsync(CitedRecoverySnapshot snapshot, string reference, CancellationToken ct) =>
        Task.FromResult<CitedRecoveryEvidence?>(null);
}

/// <summary>Read-only assessment. No ledger writer, provider client or privileged HTTP route.</summary>
public sealed class AssessCitedRecovery(ICitedRecoveryAuthorization authorization, ICitedRecoveryReader reader,
    ICitedRecoveryEvidenceVerifier verifier, IExecutionContext execution, IClock clock)
{
    public async Task<CitedRecoveryAssessment> AssessAsync(Guid reservationId, string? evidenceReference, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (execution.OrganizationId == Guid.Empty || execution.ActorId == Guid.Empty ||
                !await Safe(() => authorization.CanAssessAsync(execution.OrganizationId, execution.ActorId, deadline.Token), deadline.Token))
                throw Failure("recovery_forbidden", 403);
            if (reservationId == Guid.Empty || evidenceReference is not null && !Reference(evidenceReference))
                throw Failure("invalid_recovery_request", 400);
            var snapshot = await Safe(() => reader.ReadAsync(execution.OrganizationId, execution.ActorId, reservationId, deadline.Token), deadline.Token);
            if (snapshot is null || snapshot.OrganizationId != execution.OrganizationId || snapshot.ReservationId != reservationId)
                throw Failure("recovery_not_found", 404);
            var evidence = evidenceReference is null ? null : await Safe(() => verifier.VerifyAsync(snapshot, evidenceReference, deadline.Token), deadline.Token);
            if (evidenceReference is not null && (evidence is null || evidence.Reference != evidenceReference))
                throw Failure("recovery_evidence_unavailable");
            // A slow lookup must not preserve a revoked review privilege.
            if (!await Safe(() => authorization.CanAssessAsync(execution.OrganizationId, execution.ActorId, deadline.Token), deadline.Token))
                throw Failure("recovery_forbidden", 403);
            deadline.Token.ThrowIfCancellationRequested();
            return CitedRecoveryPolicy.Assess(snapshot, execution.ActorId, evidence, clock.UtcNow);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (CitedWorkflowException) { throw; }
        catch { throw Failure("recovery_unavailable", 503); }
    }

    private static async Task<T> Safe<T>(Func<Task<T>> action, CancellationToken ct)
    {
        try { return await action().WaitAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch { throw Failure("recovery_unavailable", 503); }
    }
    internal static bool Reference(string? value) => value is { Length: >= 1 and <= 100 } &&
        value.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or ':');
    internal static CitedWorkflowException Failure(string code, int status = 409) =>
        new(code, "Recovery assessment was not available.", status);
}

/// <summary>Not authorization and not executable. A future writer must recheck everything under lock.</summary>
public static class CitedRecoveryPolicy
{
    public static CitedRecoveryAssessment Assess(CitedRecoverySnapshot snapshot, Guid operatorActorId,
        CitedRecoveryEvidence? evidence, DateTimeOffset now)
    {
        if (snapshot is null || operatorActorId == Guid.Empty || snapshot.ReservationId == Guid.Empty ||
            snapshot.OrganizationId == Guid.Empty || snapshot.OriginalActorId == Guid.Empty || snapshot.OperationId == Guid.Empty ||
            !Hash(snapshot.RequestSha256) || !Identifier(snapshot.Provider) || !Identifier(snapshot.Model) ||
            !Identifier(snapshot.ModelVersion) || !Enum.IsDefined(snapshot.State) || snapshot.EventSequence < 1 ||
            snapshot.ReservedUsd is <= 0 or > 8 || decimal.Round(snapshot.ReservedUsd, 6) != snapshot.ReservedUsd ||
            snapshot.CreatedAtUtc > now ||
            (snapshot.State == CitedSpendState.Settled ? snapshot.ActualUsd is null || !Amount(snapshot.ActualUsd.Value) : snapshot.ActualUsd is not null))
            throw AssessCitedRecovery.Failure("invalid_recovery_snapshot");

        CitedRecoveryAssessment Result(CitedRecoveryRecommendation action, string reason, decimal? amount = null) =>
            new(snapshot.ReservationId, snapshot.OrganizationId, operatorActorId, snapshot.OriginalActorId,
                snapshot.State, snapshot.EventSequence, action, reason, evidence?.EvidenceId, amount, amount > snapshot.ReservedUsd);

        if (snapshot.State == CitedSpendState.HeldUnknown)
        {
            if (evidence is not null) throw AssessCitedRecovery.Failure("recovery_terminal");
            return Result(CitedRecoveryRecommendation.NoChange, "unknown_cost_retained");
        }
        if (snapshot.State is CitedSpendState.Settled or CitedSpendState.Cancelled)
        {
            if (evidence is not null) throw AssessCitedRecovery.Failure("recovery_terminal");
            return Result(CitedRecoveryRecommendation.NoChange, "already_terminal");
        }
        if (snapshot.State == CitedSpendState.Reserved)
        {
            if (evidence is not null) throw AssessCitedRecovery.Failure("recovery_evidence_state_conflict");
            return Result(CitedRecoveryRecommendation.CancelReserved, "not_dispatched");
        }
        if (evidence is null) return Result(CitedRecoveryRecommendation.KeepHold, "verified_evidence_required");
        if (evidence.EvidenceId == Guid.Empty || !AssessCitedRecovery.Reference(evidence.Reference) ||
            evidence.OrganizationId != snapshot.OrganizationId || evidence.ReservationId != snapshot.ReservationId ||
            evidence.OperationId != snapshot.OperationId || evidence.RequestSha256 != snapshot.RequestSha256 ||
            evidence.Provider != snapshot.Provider || evidence.Model != snapshot.Model || evidence.ModelVersion != snapshot.ModelVersion ||
            !Enum.IsDefined(evidence.Kind) || evidence.Currency != "USD" || !Amount(evidence.ActualUsd) || !evidence.IsFinal ||
            evidence.VerifiedAtUtc < snapshot.CreatedAtUtc || evidence.VerifiedAtUtc > now || evidence.ValidUntilUtc <= now ||
            evidence.Kind == CitedRecoveryEvidenceKind.ConfirmedNoCharge && evidence.ActualUsd != 0)
            throw AssessCitedRecovery.Failure("invalid_recovery_evidence");
        // A zero-cost settlement remains terminal. It must never reopen dispatch.
        return Result(CitedRecoveryRecommendation.RecordVerifiedCost, "verified_final_cost", evidence.ActualUsd);
    }
    private static bool Amount(decimal amount) => amount is >= 0 and <= 999_999_999_999.999999m && decimal.Round(amount, 6) == amount;
    private static bool Hash(string value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Identifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value == value.Trim() && !value.Any(char.IsControl);
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Institutions;

/// <summary>
/// Idempotency store for institution acknowledge requests. Lives behind
/// an abstraction so <see cref="InstitutionAcknowledgeService"/> can stay
/// in <c>Hali.Application</c> without taking a direct dependency on
/// StackExchange.Redis — mirrors the pattern used by
/// <see cref="Hali.Application.Signals.ISignalRepository"/>.
/// </summary>
public interface IInstitutionAcknowledgementStore
{
    /// <summary>
    /// Returns the previously recorded acknowledgement for the given
    /// institution + cluster + idempotency-key triplet, or null if no
    /// acknowledgement has been stored yet. Cheap optimistic fast-path —
    /// callers must still use <see cref="TryClaimAsync"/> to reserve the
    /// key before emitting an outbox event.
    /// </summary>
    Task<InstitutionAcknowledgementReplay?> TryGetReplayAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Atomically claims the idempotency key for the supplied candidate
    /// acknowledgement. Returns the authoritative winner — the caller's
    /// candidate when the claim succeeded, or the previously stored
    /// descriptor when a concurrent writer had already stored one under
    /// the same key. The second return value is <c>true</c> iff the
    /// caller's candidate won the race; <c>false</c> means the caller
    /// must NOT emit a new outbox event (a previous writer already did).
    /// Closes the pre-#207-Phase-4 race where two callers could both
    /// miss <see cref="TryGetReplayAsync"/> and each write a distinct
    /// outbox row before either claim was visible.
    /// </summary>
    Task<(InstitutionAcknowledgementReplay Winner, bool Claimed)> TryClaimAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        Guid candidateAcknowledgementId,
        DateTime candidateRecordedAt,
        CancellationToken ct);

    /// <summary>
    /// Releases a previously successful claim so a caller can safely retry
    /// the outbox write. Invoked by <see cref="InstitutionAcknowledgeService"/>
    /// when <c>WriteOutboxEventAsync</c> throws after a claim has already
    /// been recorded — without this, subsequent retries would hit the
    /// replay fast-path and silently drop the <c>institution.action.recorded</c>
    /// event. No-op when the key is not present.
    /// </summary>
    Task ReleaseClaimAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        CancellationToken ct);
}

/// <summary>
/// Replay descriptor returned by <see cref="IInstitutionAcknowledgementStore"/>.
/// </summary>
public sealed record InstitutionAcknowledgementReplay(
    Guid AcknowledgementId,
    DateTime RecordedAt);

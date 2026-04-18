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
    /// acknowledgement has been stored yet.
    /// </summary>
    Task<InstitutionAcknowledgementReplay?> TryGetReplayAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Persists the acknowledgement descriptor for future replay. Writes
    /// are idempotent — if a concurrent write has already stored a
    /// different descriptor, the existing entry is left in place.
    /// </summary>
    Task StoreAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        Guid acknowledgementId,
        DateTime recordedAt,
        CancellationToken ct);
}

/// <summary>
/// Replay descriptor returned by <see cref="IInstitutionAcknowledgementStore"/>.
/// </summary>
public sealed record InstitutionAcknowledgementReplay(
    Guid AcknowledgementId,
    DateTime RecordedAt);

using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Institutions;

namespace Hali.Application.Institutions;

/// <summary>
/// Records explicit institution acknowledgements on citizen-side signal
/// clusters. Separate from <see cref="IInstitutionReadService"/> because
/// this is a write path (emits an <c>institution.action.recorded</c>
/// outbox event) and obeys the mutation-endpoint idempotency contract.
/// Passive view tracking is intentionally deferred — see the Phase 4
/// integration audit P1 backlog.
/// </summary>
public interface IInstitutionAcknowledgeService
{
    /// <summary>
    /// Records (or idempotently replays) an acknowledgement by the given
    /// institution on the given cluster. Returns the acknowledgement
    /// record descriptor the controller surfaces on the wire.
    /// </summary>
    /// <exception cref="Hali.Application.Errors.NotFoundException">
    /// The cluster does not exist OR falls outside the caller's
    /// jurisdiction — the 404 is deliberate so one institution cannot
    /// probe for the existence of another's clusters.
    /// </exception>
    /// <exception cref="Hali.Application.Errors.ValidationException">
    /// The idempotency key is missing or blank.
    /// </exception>
    Task<InstitutionAcknowledgeResponseDto> AcknowledgeAsync(
        Guid institutionId,
        Guid clusterId,
        InstitutionAcknowledgeRequestDto request,
        CancellationToken ct);
}

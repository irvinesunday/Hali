using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Notifications;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public interface IFollowService
{
    /// <summary>
    /// Returns the raw follows for an account (no locality join).
    /// Used by internal pipelines (notifications fan-out).
    /// </summary>
    Task<IReadOnlyList<Follow>> GetFollowedAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Returns followed localities projected with their display label and
    /// canonical ward/city names. Suitable for the citizen GET response.
    /// </summary>
    Task<IReadOnlyList<FollowedLocalityDto>> GetFollowedWithDetailsAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Replaces the account's followed localities. Enforces max 5.
    /// Throws InvalidOperationException("MAX_FOLLOWED_LOCALITIES_EXCEEDED") if over limit.
    /// </summary>
    Task SetFollowedAsync(Guid accountId, IEnumerable<FollowEntry> entries, CancellationToken ct = default);
}

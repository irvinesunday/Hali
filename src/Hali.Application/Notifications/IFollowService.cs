using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public interface IFollowService
{
    Task<IReadOnlyList<Follow>> GetFollowedAsync(Guid accountId, CancellationToken ct = default);
    /// <summary>
    /// Replaces the account's followed localities. Enforces max 5.
    /// Throws InvalidOperationException("MAX_FOLLOWED_LOCALITIES_EXCEEDED") if over limit.
    /// </summary>
    Task SetFollowedAsync(Guid accountId, IEnumerable<Guid> localityIds, CancellationToken ct = default);
}

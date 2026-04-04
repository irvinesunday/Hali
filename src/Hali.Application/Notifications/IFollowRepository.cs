using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public interface IFollowRepository
{
    Task<IReadOnlyList<Follow>> GetByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Follow>> GetByLocalityAsync(Guid localityId, CancellationToken ct = default);
    Task<int> CountByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task ReplaceFollowsAsync(Guid accountId, IEnumerable<Guid> localityIds, CancellationToken ct = default);
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Advisories;

namespace Hali.Application.Advisories;

public interface IOfficialPostRepository
{
    Task<OfficialPost> CreateAsync(OfficialPost post, OfficialPostScope scope, CancellationToken ct);
    /// <summary>
    /// Pre-insert jurisdiction check. Returns true when the institution is
    /// allowed to post for the given locality (or has no jurisdiction rows).
    /// </summary>
    Task<bool> CheckJurisdictionForLocalityAsync(Guid institutionId, Guid? localityId, CancellationToken ct);
    Task<bool> JurisdictionIntersectsScopeAsync(Guid institutionId, Guid postId, CancellationToken ct);
    Task<List<OfficialPost>> GetByClusterIdAsync(Guid clusterId, CancellationToken ct);
    Task<List<OfficialPost>> GetActiveByLocalityAsync(Guid localityId, CancellationToken ct);
    Task<int> ExpirePostsAsync(CancellationToken ct);
}

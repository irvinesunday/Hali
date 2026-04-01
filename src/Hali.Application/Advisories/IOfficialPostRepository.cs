using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Advisories;

namespace Hali.Application.Advisories;

public interface IOfficialPostRepository
{
    Task<OfficialPost> CreateAsync(OfficialPost post, OfficialPostScope scope, CancellationToken ct);
    Task<bool> JurisdictionIntersectsScopeAsync(Guid institutionId, Guid postId, CancellationToken ct);
    Task<List<OfficialPost>> GetByClusterIdAsync(Guid clusterId, CancellationToken ct);
    Task<List<OfficialPost>> GetActiveByLocalityAsync(Guid localityId, CancellationToken ct);
    Task<int> ExpirePostsAsync(CancellationToken ct);
}

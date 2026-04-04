using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public class FollowService : IFollowService
{
    private const int MaxFollowedLocalities = 5;
    private readonly IFollowRepository _repo;

    public FollowService(IFollowRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<Follow>> GetFollowedAsync(Guid accountId, CancellationToken ct = default)
        => _repo.GetByAccountAsync(accountId, ct);

    public async Task SetFollowedAsync(Guid accountId, IEnumerable<Guid> localityIds, CancellationToken ct = default)
    {
        var ids = localityIds.Distinct().ToList();
        if (ids.Count > MaxFollowedLocalities)
            throw new InvalidOperationException("MAX_FOLLOWED_LOCALITIES_EXCEEDED");

        await _repo.ReplaceFollowsAsync(accountId, ids, ct);
    }
}

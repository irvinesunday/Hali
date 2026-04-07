using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Signals;
using Hali.Contracts.Notifications;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public class FollowService : IFollowService
{
    private const int MaxFollowedLocalities = 5;
    private readonly IFollowRepository _repo;
    private readonly ILocalityLookupRepository _localities;

    public FollowService(IFollowRepository repo, ILocalityLookupRepository localities)
    {
        _repo = repo;
        _localities = localities;
    }

    public Task<IReadOnlyList<Follow>> GetFollowedAsync(Guid accountId, CancellationToken ct = default)
        => _repo.GetByAccountAsync(accountId, ct);

    public async Task<IReadOnlyList<FollowedLocalityDto>> GetFollowedWithDetailsAsync(
        Guid accountId,
        CancellationToken ct = default)
    {
        var follows = await _repo.GetByAccountAsync(accountId, ct);
        if (follows.Count == 0)
            return Array.Empty<FollowedLocalityDto>();

        var ids = follows.Select(f => f.LocalityId).Distinct().ToList();
        var lookup = await _localities.GetByIdsAsync(ids, ct);

        return follows
            .Select(f =>
            {
                lookup.TryGetValue(f.LocalityId, out var summary);
                return new FollowedLocalityDto
                {
                    LocalityId = f.LocalityId,
                    DisplayLabel = f.DisplayLabel,
                    WardName = summary?.WardName ?? string.Empty,
                    CityName = summary?.CityName,
                };
            })
            .ToList();
    }

    public async Task SetFollowedAsync(Guid accountId, IEnumerable<FollowEntry> entries, CancellationToken ct = default)
    {
        var deduped = entries
            .GroupBy(e => e.LocalityId)
            .Select(g => g.First())
            .ToList();

        if (deduped.Count > MaxFollowedLocalities)
            throw new InvalidOperationException("MAX_FOLLOWED_LOCALITIES_EXCEEDED");

        await _repo.ReplaceFollowsAsync(accountId, deduped, ct);
    }
}

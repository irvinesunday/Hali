using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
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

        // Drop follows whose locality can no longer be resolved from the
        // Signals DB. This can only happen if a locality row was deleted
        // out from under us; returning a blank wardName would violate the
        // API contract (wardName is required) and confuse the UI.
        return follows
            .Where(f => lookup.ContainsKey(f.LocalityId))
            .Select(f =>
            {
                var summary = lookup[f.LocalityId];
                return new FollowedLocalityDto
                {
                    LocalityId = f.LocalityId,
                    DisplayLabel = f.DisplayLabel,
                    WardName = summary.WardName,
                    CityName = summary.CityName,
                };
            })
            .ToList();
    }

    public async Task SetFollowedAsync(Guid accountId, IEnumerable<FollowEntry> entries, CancellationToken ct = default)
    {
        // When the same localityId appears multiple times in the request,
        // prefer the first entry that carries a non-empty DisplayLabel so
        // a later null/blank entry does not silently clobber a meaningful
        // label. Falls back to the first entry if none carry a label.
        var deduped = entries
            .GroupBy(e => e.LocalityId)
            .Select(g => g.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.DisplayLabel)) ?? g.First())
            .ToList();

        if (deduped.Count > MaxFollowedLocalities)
            throw new ValidationException(
                "You may follow at most 5 localities.",
                code: ErrorCodes.ValidationMaxFollowedLocalitiesExceeded);

        await _repo.ReplaceFollowsAsync(accountId, deduped, ct);
    }
}

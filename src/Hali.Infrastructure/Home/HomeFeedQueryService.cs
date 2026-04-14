using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Clusters;
using Hali.Application.Home;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Clusters;
using Microsoft.Extensions.DependencyInjection;

namespace Hali.Infrastructure.Home;

/// <summary>
/// Read-only home feed query service. Each method creates a new DI scope and
/// resolves the scoped canonical repository/service implementations from that
/// scope, then delegates. Per-call scope isolation keeps concurrent section
/// reads safe — no two tasks share a DbContext instance. Canonical paths
/// already use AsNoTracking, so this façade inherits that posture.
/// </summary>
public class HomeFeedQueryService : IHomeFeedQueryService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public HomeFeedQueryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(
        IEnumerable<Guid> localityIds, bool? recurringOnly, int limit,
        DateTime? cursorBefore, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var clusters = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        return await clusters.GetActiveByLocalitiesPagedAsync(localityIds, recurringOnly, limit, cursorBefore, ct);
    }

    public async Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(
        IEnumerable<Guid> excludeLocalityIds, int limit,
        DateTime? cursorBefore, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var clusters = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        return await clusters.GetAllActivePagedAsync(excludeLocalityIds, limit, cursorBefore, ct);
    }

    public async Task<IReadOnlyList<OfficialPostResponseDto>> GetOfficialPostsByLocalitiesAsync(
        IEnumerable<Guid> localityIds, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var officialPosts = scope.ServiceProvider.GetRequiredService<IOfficialPostsService>();
        return await officialPosts.GetActiveByLocalitiesAsync(localityIds, ct);
    }
}

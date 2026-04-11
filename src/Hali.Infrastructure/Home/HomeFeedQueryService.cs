using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Home;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Clusters;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Clusters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hali.Infrastructure.Home;

/// <summary>
/// Read-only home feed query service. Each method creates a new DI scope
/// and resolves a fresh DbContext, making concurrent calls safe — no two
/// tasks share a DbContext instance. All queries use AsNoTracking.
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
        var db = scope.ServiceProvider.GetRequiredService<ClustersDbContext>();

        var ids = localityIds.ToList();
        var q = db.SignalClusters
            .AsNoTracking()
            .Where(c => c.LocalityId != null && ids.Contains(c.LocalityId.Value) && (int)c.State == 1);

        if (recurringOnly == true)
            q = q.Where(c => c.TemporalType == "recurring");
        else if (recurringOnly == false)
            q = q.Where(c => c.TemporalType != "recurring");

        if (cursorBefore.HasValue)
            q = q.Where(c => c.ActivatedAt < cursorBefore.Value);

        return await q.OrderByDescending(c => c.ActivatedAt).Take(limit).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(
        IEnumerable<Guid> excludeLocalityIds, int limit,
        DateTime? cursorBefore, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ClustersDbContext>();

        var excludeIds = excludeLocalityIds.ToList();
        var q = db.SignalClusters
            .AsNoTracking()
            .Where(c => (int)c.State == 1 &&
                        (c.LocalityId == null || !excludeIds.Contains(c.LocalityId.Value)));

        if (cursorBefore.HasValue)
            q = q.Where(c => c.ActivatedAt < cursorBefore.Value);

        return await q.OrderByDescending(c => c.ActivatedAt).Take(limit).ToListAsync(ct);
    }

    public async Task<List<OfficialPostResponseDto>> GetOfficialPostsByLocalityAsync(
        Guid localityId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdvisoriesDbContext>();

        var postIds = await db.OfficialPostScopes
            .AsNoTracking()
            .Where(s => s.LocalityId == localityId)
            .Select(s => s.OfficialPostId)
            .Distinct()
            .ToListAsync(ct);

        var posts = await db.OfficialPosts
            .AsNoTracking()
            .Where(p => postIds.Contains(p.Id) && p.Status == "published")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        return posts.Select(MapToDto).ToList();
    }

    // ── DTO mapping (mirrors OfficialPostsService.MapToDto exactly) ─────────

    private static string EnumToSnakeCase(string name) =>
        Regex.Replace(name, "(?<=[a-z])([A-Z])", "_$1").ToLowerInvariant();

    private static OfficialPostResponseDto MapToDto(OfficialPost p) => new(
        p.Id,
        p.InstitutionId,
        EnumToSnakeCase(p.Type.ToString()),
        EnumToSnakeCase(p.Category.ToString()),
        p.Title,
        p.Body,
        p.StartsAt,
        p.EndsAt,
        p.Status,
        p.RelatedClusterId,
        p.IsRestorationClaim,
        p.CreatedAt);
}

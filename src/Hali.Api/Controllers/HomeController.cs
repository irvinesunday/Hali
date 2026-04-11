using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Clusters;
using Hali.Application.Notifications;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/home")]
public class HomeController : ControllerBase
{
    private const int LimitActiveNow = 20;
    private const int LimitOfficialUpdates = 5;
    private const int LimitRecurring = 10;
    private const int LimitOtherActive = 10;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly IClusterRepository _clusters;
    private readonly IOfficialPostsService _officialPosts;
    private readonly IFollowService _follows;
    private readonly IDatabase _redis;

    public HomeController(
        IClusterRepository clusters,
        IOfficialPostsService officialPosts,
        IFollowService follows,
        IDatabase redis)
    {
        _clusters = clusters;
        _officialPosts = officialPosts;
        _follows = follows;
        _redis = redis;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetHome(
        [FromQuery] string? section,
        [FromQuery] string? cursor,
        [FromQuery] Guid? localityId,
        CancellationToken ct)
    {
        // Only authenticated users may scope the feed to an explicit locality.
        // Anonymous callers fall back to the followed-localities path, which
        // returns empty sections for guests.
        bool isAuthenticated = User.Identity?.IsAuthenticated == true;
        var localityIds = localityId.HasValue && isAuthenticated
            ? new List<Guid> { localityId.Value }
            : await GetFollowedLocalityIdsAsync(ct);
        var cursorDt = DecodeCursor(cursor);

        // Section-specific paginated request — skip cache, return single section
        if (section is not null)
        {
            var paged = await GetPagedSectionAsync(section, localityIds, cursorDt, ct);
            if (paged is null) return BadRequest(new { error = "Unknown section name" });
            return Ok(paged);
        }

        // Full home response — try Redis cache for first page
        if (cursor is null && localityIds.Count > 0)
        {
            var cacheKey = BuildCacheKey(localityIds);
            RedisValue cached = await _redis.StringGetAsync(cacheKey);
            if (cached.HasValue)
                return Content(cached!, "application/json");

            var response = await BuildFullResponseAsync(localityIds, ct);
            var json = JsonSerializer.Serialize(response);
            await _redis.StringSetAsync(cacheKey, json, CacheTtl);
            return Ok(response);
        }

        return Ok(await BuildFullResponseAsync(localityIds, ct));
    }

    // ── Section helpers ──────────────────────────────────────────────────────

    private async Task<object?> GetPagedSectionAsync(
        string section, List<Guid> localityIds, DateTime? cursorDt, CancellationToken ct)
    {
        return section switch
        {
            "active_now" => await BuildActiveNowSectionAsync(localityIds, cursorDt, ct),
            "official_updates" => await BuildOfficialUpdatesSectionAsync(localityIds, cursorDt, ct),
            "recurring_at_this_time" => await BuildRecurringSectionAsync(localityIds, cursorDt, ct),
            "other_active_signals" => await BuildOtherActiveSectionAsync(localityIds, cursorDt, ct),
            _ => null
        };
    }

    private async Task<HomeResponseDto> BuildFullResponseAsync(List<Guid> localityIds, CancellationToken ct)
    {
        var activeNowTask = BuildActiveNowSectionAsync(localityIds, null, ct);
        var officialUpdatesTask = BuildOfficialUpdatesSectionAsync(localityIds, null, ct);
        var recurringTask = BuildRecurringSectionAsync(localityIds, null, ct);
        var otherActiveTask = BuildOtherActiveSectionAsync(localityIds, null, ct);

        await Task.WhenAll(activeNowTask, officialUpdatesTask, recurringTask, otherActiveTask);

        return new HomeResponseDto
        {
            ActiveNow = activeNowTask.Result,
            OfficialUpdates = officialUpdatesTask.Result,
            RecurringAtThisTime = recurringTask.Result,
            OtherActiveSignals = otherActiveTask.Result
        };
    }

    private async Task<PagedSection<ClusterResponseDto>> BuildActiveNowSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        if (localityIds.Count == 0)
            return EmptyClusterSection();

        var raw = await _clusters.GetActiveByLocalitiesPagedAsync(
            localityIds, recurringOnly: false, limit: LimitActiveNow + 1, cursorBefore, ct);

        return ToPagedClusterSection(raw, LimitActiveNow);
    }

    private async Task<PagedSection<OfficialPostResponseDto>> BuildOfficialUpdatesSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        if (localityIds.Count == 0)
            return EmptyPostSection();

        var postTasks = localityIds.Select(lid => _officialPosts.GetActiveByLocalityAsync(lid, ct));
        var postResults = await Task.WhenAll(postTasks);
        var allPosts = postResults.SelectMany(p => p).ToList();

        // Sort by CreatedAt descending, apply cursor
        var sorted = allPosts
            .OrderByDescending(p => p.CreatedAt)
            .Where(p => cursorBefore is null || p.CreatedAt < cursorBefore.Value)
            .Take(LimitOfficialUpdates + 1)
            .ToList();

        bool hasMore = sorted.Count > LimitOfficialUpdates;
        var items = sorted.Take(LimitOfficialUpdates).ToList();
        string? nextCursor = hasMore ? EncodeCursor(items.Last().CreatedAt) : null;

        return new PagedSection<OfficialPostResponseDto>
        {
            Items = items,
            NextCursor = nextCursor,
            TotalCount = allPosts.Count
        };
    }

    private async Task<PagedSection<ClusterResponseDto>> BuildRecurringSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        if (localityIds.Count == 0)
            return EmptyClusterSection();

        var raw = await _clusters.GetActiveByLocalitiesPagedAsync(
            localityIds, recurringOnly: true, limit: LimitRecurring + 1, cursorBefore, ct);

        return ToPagedClusterSection(raw, LimitRecurring);
    }

    private async Task<PagedSection<ClusterResponseDto>> BuildOtherActiveSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        var raw = await _clusters.GetAllActivePagedAsync(localityIds, LimitOtherActive + 1, cursorBefore, ct);
        return ToPagedClusterSection(raw, LimitOtherActive);
    }

    // ── Pagination utilities ─────────────────────────────────────────────────

    private static PagedSection<ClusterResponseDto> ToPagedClusterSection(
        IReadOnlyList<SignalCluster> raw, int limit)
    {
        bool hasMore = raw.Count > limit;
        var items = raw.Take(limit).Select(ToDto).ToList();
        string? nextCursor = hasMore ? EncodeCursor(raw[limit - 1].ActivatedAt) : null;

        return new PagedSection<ClusterResponseDto>
        {
            Items = items,
            NextCursor = nextCursor,
            TotalCount = raw.Count
        };
    }

    private static string? EncodeCursor(DateTime? dt)
    {
        if (dt is null) return null;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(dt.Value.Ticks.ToString()));
    }

    private static DateTime? DecodeCursor(string? cursor)
    {
        if (cursor is null) return null;
        try
        {
            var ticks = long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            return new DateTime(ticks, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCacheKey(List<Guid> localityIds)
    {
        var sorted = string.Join(",", localityIds.OrderBy(g => g));
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(sorted)));
        return $"home:{hash}:p1";
    }

    // ── Auth / follow helpers ─────────────────────────────────────────────────

    private async Task<List<Guid>> GetFollowedLocalityIdsAsync(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true) return [];
        var raw = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        if (!Guid.TryParse(raw, out var accountId)) return [];
        var follows = await _follows.GetFollowedAsync(accountId, ct);
        return follows.Select(f => f.LocalityId).ToList();
    }

    // ── DTO mapping ───────────────────────────────────────────────────────────

    private static ClusterResponseDto ToDto(SignalCluster c) =>
        new ClusterResponseDto(
            c.Id,
            c.State.ToString().ToLowerInvariant(),
            c.Category.ToString().ToLowerInvariant(),
            c.SubcategorySlug,
            c.Title,
            c.Summary,
            c.AffectedCount,
            c.ObservingCount,
            c.CreatedAt,
            c.UpdatedAt,
            c.ActivatedAt,
            c.PossibleRestorationAt,
            c.ResolvedAt);

    private static PagedSection<ClusterResponseDto> EmptyClusterSection() =>
        new() { Items = [], NextCursor = null, TotalCount = 0 };

    private static PagedSection<OfficialPostResponseDto> EmptyPostSection() =>
        new() { Items = [], NextCursor = null, TotalCount = 0 };
}

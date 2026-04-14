using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Application.Home;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    private readonly IHomeFeedQueryService _feedQuery;
    private readonly IFollowService _follows;
    private readonly IDatabase _redis;
    private readonly ILogger<HomeController>? _logger;

    /// <summary>
    /// JSON options sourced from the MVC-configured serializer so that the
    /// cached response path uses the same camelCase property naming and
    /// snake_case enum serialization as the non-cached Ok() path. Reusing
    /// <see cref="Microsoft.AspNetCore.Mvc.JsonOptions"/> from DI (instead
    /// of a controller-local copy) prevents silent drift if the global JSON
    /// configuration changes — the cache will automatically track it.
    /// </summary>
    private readonly JsonSerializerOptions _cacheJsonOptions;

    public HomeController(
        IHomeFeedQueryService feedQuery,
        IFollowService follows,
        IDatabase redis,
        IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> mvcJsonOptions,
        ILogger<HomeController>? logger = null)
    {
        _feedQuery = feedQuery;
        _follows = follows;
        _redis = redis;
        _cacheJsonOptions = mvcJsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetHome(
        [FromQuery] string? section,
        [FromQuery] string? cursor,
        [FromQuery] Guid? localityId,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("{EventName}", ObservabilityEvents.HomeRequestStarted);

        try
        {
            // Both authenticated and anonymous callers may scope the feed to
            // an explicit locality via ?localityId.  When omitted,
            // authenticated users fall back to their followed-localities set;
            // anonymous callers receive empty sections (no followed wards).
            bool isAuthenticated = User.Identity?.IsAuthenticated == true;
            var localityIds = localityId.HasValue
                ? new List<Guid> { localityId.Value }
                : await GetFollowedLocalityIdsAsync(ct);
            var cursorDt = DecodeCursor(cursor);

            // Log locality scoping mode and auth posture
            if (localityId.HasValue)
                _logger?.LogInformation("{EventName} localityId={LocalityId} isAuthenticated={IsAuthenticated}",
                    ObservabilityEvents.HomeLocalityScopeExplicit, localityId.Value, isAuthenticated);
            else if (localityIds.Count > 0)
                _logger?.LogInformation("{EventName} localityCount={LocalityCount}",
                    ObservabilityEvents.HomeLocalityScopeFallback, localityIds.Count);
            else
                _logger?.LogInformation("{EventName} isAuthenticated={IsAuthenticated}",
                    ObservabilityEvents.HomeLocalityScopeGuestEmpty, isAuthenticated);

            // Section-specific paginated request — skip cache, return single section
            if (section is not null)
            {
                var paged = await GetPagedSectionAsync(section, localityIds, cursorDt, ct);
                string safeSection = ObservabilityEvents.SanitizeForLog(section);
                if (paged is null)
                {
                    sw.Stop();
                    _logger?.LogInformation(
                        "{EventName} section={Section} statusCode={StatusCode} durationMs={DurationMs}",
                        ObservabilityEvents.HomeRequestCompleted, safeSection, 400, sw.ElapsedMilliseconds);
                    throw new ValidationException("Unknown section name.", code: "validation.invalid_section");
                }

                sw.Stop();
                _logger?.LogInformation(
                    "{EventName} section={Section} durationMs={DurationMs}",
                    ObservabilityEvents.HomeRequestCompleted, safeSection, sw.ElapsedMilliseconds);
                return Ok(paged);
            }

            // Full home response — try Redis cache for first page
            if (cursor is null && localityIds.Count > 0)
            {
                var cacheKey = BuildCacheKey(localityIds);
                _logger?.LogInformation("{EventName}", ObservabilityEvents.HomeCacheChecked);

                RedisValue cached = await _redis.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    sw.Stop();
                    _logger?.LogInformation("{EventName} durationMs={DurationMs}",
                        ObservabilityEvents.HomeCacheHit, sw.ElapsedMilliseconds);
                    _logger?.LogInformation(
                        "{EventName} durationMs={DurationMs} cacheHit={CacheHit}",
                        ObservabilityEvents.HomeRequestCompleted, sw.ElapsedMilliseconds, true);
                    return Content(cached!, "application/json");
                }

                _logger?.LogInformation("{EventName}", ObservabilityEvents.HomeCacheMiss);

                var response = await BuildFullResponseAsync(localityIds, ct);

                // Only authenticated callers write to cache.  Anonymous
                // callers can supply arbitrary localityId GUIDs; letting
                // them populate cache entries would allow unbounded key
                // creation.  They still benefit from cache hits written
                // by authenticated users for the same locality.
                if (isAuthenticated)
                {
                    var json = JsonSerializer.Serialize(response, _cacheJsonOptions);
                    await _redis.StringSetAsync(cacheKey, json, CacheTtl);
                }

                sw.Stop();
                _logger?.LogInformation(
                    "{EventName} durationMs={DurationMs} cacheHit={CacheHit}",
                    ObservabilityEvents.HomeRequestCompleted, sw.ElapsedMilliseconds, false);
                return Ok(response);
            }

            var fullResponse = await BuildFullResponseAsync(localityIds, ct);
            sw.Stop();
            _logger?.LogInformation(
                "{EventName} durationMs={DurationMs}",
                ObservabilityEvents.HomeRequestCompleted, sw.ElapsedMilliseconds);
            return Ok(fullResponse);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger?.LogError(ex,
                "{EventName} durationMs={DurationMs}",
                ObservabilityEvents.HomeRequestFailed, sw.ElapsedMilliseconds);
            throw;
        }
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
        // Each section task is safe to run concurrently because
        // IHomeFeedQueryService creates an isolated DbContext per call.
        var activeNowTask = BuildTimedSectionAsync("active_now",
            () => BuildActiveNowSectionAsync(localityIds, null, ct));
        var officialUpdatesTask = BuildTimedSectionAsync("official_updates",
            () => BuildOfficialUpdatesSectionAsync(localityIds, null, ct));
        var recurringTask = BuildTimedSectionAsync("recurring_at_this_time",
            () => BuildRecurringSectionAsync(localityIds, null, ct));
        var otherActiveTask = BuildTimedSectionAsync("other_active_signals",
            () => BuildOtherActiveSectionAsync(localityIds, null, ct));

        await Task.WhenAll(activeNowTask, officialUpdatesTask, recurringTask, otherActiveTask);

        return new HomeResponseDto
        {
            ActiveNow = await activeNowTask,
            OfficialUpdates = await officialUpdatesTask,
            RecurringAtThisTime = await recurringTask,
            OtherActiveSignals = await otherActiveTask
        };
    }

    private async Task<T> BuildTimedSectionAsync<T>(string sectionName, Func<Task<T>> buildFunc)
    {
        var sw = Stopwatch.StartNew();
        var result = await buildFunc();
        sw.Stop();

        int itemCount = result switch
        {
            PagedSection<ClusterResponseDto> cs => cs.Items.Count,
            PagedSection<OfficialPostResponseDto> ps => ps.Items.Count,
            _ => 0
        };

        _logger?.LogInformation(
            "{EventName} section={Section} itemCount={ItemCount} durationMs={DurationMs}",
            ObservabilityEvents.HomeSectionBuilt, sectionName, itemCount, sw.ElapsedMilliseconds);

        return result;
    }

    private async Task<PagedSection<ClusterResponseDto>> BuildActiveNowSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        if (localityIds.Count == 0)
            return EmptyClusterSection();

        var raw = await _feedQuery.GetActiveByLocalitiesPagedAsync(
            localityIds, recurringOnly: false, limit: LimitActiveNow + 1, cursorBefore, ct);

        return ToPagedClusterSection(raw, LimitActiveNow);
    }

    private async Task<PagedSection<OfficialPostResponseDto>> BuildOfficialUpdatesSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        if (localityIds.Count == 0)
            return EmptyPostSection();

        IReadOnlyList<OfficialPostResponseDto> allPosts =
            await _feedQuery.GetOfficialPostsByLocalitiesAsync(localityIds, ct);

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

        var raw = await _feedQuery.GetActiveByLocalitiesPagedAsync(
            localityIds, recurringOnly: true, limit: LimitRecurring + 1, cursorBefore, ct);

        return ToPagedClusterSection(raw, LimitRecurring);
    }

    private async Task<PagedSection<ClusterResponseDto>> BuildOtherActiveSectionAsync(
        List<Guid> localityIds, DateTime? cursorBefore, CancellationToken ct)
    {
        if (localityIds.Count == 0)
            return EmptyClusterSection();

        var raw = await _feedQuery.GetAllActivePagedAsync(localityIds, LimitOtherActive + 1, cursorBefore, ct);
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
            JsonNamingPolicy.SnakeCaseLower.ConvertName(c.State.ToString()),
            JsonNamingPolicy.SnakeCaseLower.ConvertName(c.Category.ToString()),
            c.SubcategorySlug,
            c.Title,
            c.Summary,
            c.AffectedCount,
            c.ObservingCount,
            c.CreatedAt,
            c.UpdatedAt,
            c.ActivatedAt,
            c.PossibleRestorationAt,
            c.ResolvedAt)
        {
            LocationLabel = c.LocationLabelText
        };

    private static PagedSection<ClusterResponseDto> EmptyClusterSection() =>
        new() { Items = [], NextCursor = null, TotalCount = 0 };

    private static PagedSection<OfficialPostResponseDto> EmptyPostSection() =>
        new() { Items = [], NextCursor = null, TotalCount = 0 };
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Logging;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Notifications;
using Hali.Application.Signals;
using Hali.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/localities")]
[Authorize]
public class LocalitiesController : ControllerBase
{
    private readonly IFollowService _follows;
    private readonly IGeocodingService _geocoding;
    private readonly ILocalityLookupRepository _localities;
    private readonly IDatabase _redis;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<LocalitiesController> _logger;

    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(1);
    private const int MaxSearchQueryLength = 80;
    private const int SearchRateLimitMaxRequests = 30;
    private static readonly TimeSpan SearchRateLimitWindow = TimeSpan.FromMinutes(1);

    public LocalitiesController(
        IFollowService follows,
        IGeocodingService geocoding,
        ILocalityLookupRepository localities,
        IDatabase redis,
        IRateLimiter rateLimiter,
        ILogger<LocalitiesController> logger)
    {
        _follows = follows;
        _geocoding = geocoding;
        _localities = localities;
        _redis = redis;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    [HttpGet("followed")]
    public async Task<IActionResult> GetFollowed(CancellationToken ct)
    {
        var accountId = GetAccountId()
            ?? throw new UnauthorizedException();

        var followed = await _follows.GetFollowedWithDetailsAsync(accountId, ct);
        return Ok(followed);
    }

    [HttpPut("followed")]
    public async Task<IActionResult> SetFollowed(
        [FromBody] FollowedLocalitiesRequestDto dto,
        CancellationToken ct)
    {
        var accountId = GetAccountId()
            ?? throw new UnauthorizedException();

        // Prefer the items shape (carries displayLabel). Fall back to legacy
        // localityIds shape so existing clients still work.
        var entries = dto.Items.Count > 0
            ? dto.Items.Select(i => new FollowEntry(i.LocalityId, i.DisplayLabel))
            : dto.LocalityIds.Select(id => new FollowEntry(id, null));

        await _follows.SetFollowedAsync(accountId, entries, ct);

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        var count = dto.Items.Count > 0 ? dto.Items.Count : dto.LocalityIds.Count;
        // Log a non-reversible hash of the account id rather than the raw
        // GUID (CodeQL cs/cleartext-storage-of-sensitive-information).
        _logger.LogInformation(
            "{eventName} correlationId={CorrelationId} accountHash={AccountHash} count={Count}",
            "locality.follows_updated", correlationId, AccountLogIdentifier.Hash(accountId), count);

        return NoContent();
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return BadRequest(new { error = "Query must be at least 2 characters.", code = "query_too_short" });

        var query = q.Trim();
        if (query.Length > MaxSearchQueryLength)
            return BadRequest(new { error = $"Query must be at most {MaxSearchQueryLength} characters.", code = "query_too_long" });

        // Anonymous endpoint — rate limit per client IP to bound DoS surface
        // (each call may trigger an upstream Nominatim request + PostGIS lookup).
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var rateKey = $"ratelimit:locality_search:{clientIp}";
        var allowed = await _rateLimiter.IsAllowedAsync(rateKey, SearchRateLimitMaxRequests, SearchRateLimitWindow, ct);
        if (!allowed)
        {
            return StatusCode(429, new { error = "Too many requests.", code = "rate_limited" });
        }

        var cacheKey = $"locality_search:{query.ToLowerInvariant()}";

        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                var hit = JsonSerializer.Deserialize<List<LocalitySearchResultDto>>((string)cached!);
                if (hit is not null) return Ok(hit);
            }
            catch
            {
                // fall through to live fetch
            }
        }

        IReadOnlyList<GeocodingCandidate> candidates;
        try
        {
            candidates = (await _geocoding.SearchAsync(query, ct)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Locality search geocoding failed for query (length={QueryLength})", query.Length);
            return Ok(Array.Empty<LocalitySearchResultDto>());
        }

        var results = new List<LocalitySearchResultDto>();
        foreach (var c in candidates)
        {
            var locality = await _localities.FindByPointAsync(c.Latitude, c.Longitude, ct);
            if (locality is null) continue;

            results.Add(new LocalitySearchResultDto
            {
                LocalityId = locality.Id,
                PlaceLabel = TrimLabel(c.DisplayName),
                WardName = locality.WardName,
                CityName = locality.CityName,
            });

            if (results.Count >= 5) break;
        }

        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(results), SearchCacheTtl);
        return Ok(results);
    }

    private static string TrimLabel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        // Nominatim display_name is a long comma-joined string. Keep the
        // first 2 segments which give the area + city context.
        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? raw : string.Join(", ", parts.Take(2));
    }

    private Guid? GetAccountId()
    {
        var raw = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// GET /v1/localities/resolve-by-coordinates
    /// Resolves GPS coordinates to the nearest ward.
    /// Only called when the user explicitly opts in to GPS-based locality detection.
    /// </summary>
    [HttpGet("resolve-by-coordinates")]
    [AllowAnonymous]
    public async Task<IActionResult> ResolveByCoordinates(
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken ct)
    {
        if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
            return BadRequest(new { error = "Invalid coordinates.", code = "invalid_coordinates" });

        var locality = await _localities.FindByPointAsync(lat, lng, ct);
        if (locality is null)
            return NotFound(new { error = "No locality found for the given coordinates.", code = "locality_not_found" });

        return Ok(new
        {
            localityId = locality.Id,
            wardName = locality.WardName,
            cityName = locality.CityName,
        });
    }
}

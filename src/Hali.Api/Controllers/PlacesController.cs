using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Hali.Api.Controllers;

/// <summary>
/// Low-confidence location fallback endpoints backing the C11 place-search
/// and current-location correction flows in the mobile composer.
///
/// These endpoints are anonymous because they run during signal composition
/// (the user may not yet be authenticated) and because they only return
/// information that is already public (OSM place data + Hali ward boundaries).
/// They are rate-limited per client IP to bound DoS surface on the upstream
/// Nominatim dependency and cached in Redis to keep hot queries cheap.
///
/// Spatial integrity: candidates whose coordinates fall outside any known
/// Hali locality are filtered out here so that the composer cannot surface a
/// selection the backend would later reject at <c>SubmitAsync</c>'s
/// <c>validation.locality_unresolved</c> check.
/// </summary>
[ApiController]
[Route("v1/places")]
[AllowAnonymous]
public class PlacesController : ControllerBase
{
    private readonly IGeocodingService _geocoding;
    private readonly ILocalityLookupRepository _localities;
    private readonly IDatabase _redis;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<PlacesController> _logger;

    private readonly JsonSerializerOptions _cacheJsonOptions;

    private const int MaxSearchQueryLength = 80;
    private const int MaxResults = 5;
    private const int RateLimitMaxRequests = 30;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ReverseCacheTtl = TimeSpan.FromDays(1);

    public PlacesController(
        IGeocodingService geocoding,
        ILocalityLookupRepository localities,
        IDatabase redis,
        IRateLimiter rateLimiter,
        IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> mvcJsonOptions,
        ILogger<PlacesController> logger)
    {
        _geocoding = geocoding;
        _localities = localities;
        _redis = redis;
        _rateLimiter = rateLimiter;
        _cacheJsonOptions = mvcJsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    /// <summary>
    /// GET /v1/places/search?q=...
    /// Forward geocode a free-text place query into up to 5 candidate places,
    /// each with coordinates and the Hali locality they fall inside. Used by
    /// the composer's low-confidence fallback picker.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<PlaceCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return BadRequest(new { error = "Query must be at least 2 characters.", code = "query_too_short" });
        }

        string query = q.Trim();
        if (query.Length > MaxSearchQueryLength)
        {
            return BadRequest(new { error = $"Query must be at most {MaxSearchQueryLength} characters.", code = "query_too_long" });
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string rateKey = $"ratelimit:places_search:{clientIp}";
        bool allowed = await _rateLimiter.IsAllowedAsync(rateKey, RateLimitMaxRequests, RateLimitWindow, ct);
        if (!allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too many requests.", code = "rate_limited" });
        }

        string cacheKey = $"places_search:{query.ToLowerInvariant()}";
        RedisValue cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                var hit = JsonSerializer.Deserialize<List<PlaceCandidateDto>>((string)cached!, _cacheJsonOptions);
                if (hit is not null)
                {
                    return Ok(hit);
                }
            }
            catch
            {
                // fall through to live fetch
            }
        }

        IReadOnlyList<GeocodingCandidate> candidates;
        try
        {
            candidates = await _geocoding.SearchAsync(query, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Place search geocoding failed (query length={QueryLength})", query.Length);
            return Ok(Array.Empty<PlaceCandidateDto>());
        }

        List<PlaceCandidateDto> results = new List<PlaceCandidateDto>(MaxResults);
        foreach (GeocodingCandidate c in candidates)
        {
            LocalitySummary? locality = await _localities.FindByPointAsync(c.Latitude, c.Longitude, ct);
            if (locality is null)
            {
                continue;
            }

            results.Add(new PlaceCandidateDto
            {
                // c.DisplayName is guaranteed non-null by the upstream
                // NominatimGeocodingService parser (rows with missing
                // display_name are dropped). TrimDisplayName only returns
                // null for null input, so the coalesce is defensive.
                DisplayName = TrimDisplayName(c.DisplayName) ?? c.DisplayName,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                LocalityId = locality.Id,
                WardName = locality.WardName,
                CityName = locality.CityName,
            });

            if (results.Count >= MaxResults)
            {
                break;
            }
        }

        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(results, _cacheJsonOptions), SearchCacheTtl);
        return Ok(results);
    }

    /// <summary>
    /// GET /v1/places/reverse?latitude=...&amp;longitude=...
    /// Reverse geocode a coordinate pair into a single place candidate with
    /// a human-readable label and the Hali locality the point falls inside.
    /// Used by the composer's "use my current location" fallback action.
    /// Returns 404 when the coordinates are outside any known locality —
    /// the client should fall back to place-search in that case.
    /// </summary>
    [HttpGet("reverse")]
    [ProducesResponseType(typeof(PlaceCandidateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Reverse(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken ct)
    {
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            return BadRequest(new { error = "Invalid coordinates.", code = "invalid_coordinates" });
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string rateKey = $"ratelimit:places_reverse:{clientIp}";
        bool allowed = await _rateLimiter.IsAllowedAsync(rateKey, RateLimitMaxRequests, RateLimitWindow, ct);
        if (!allowed)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Too many requests.", code = "rate_limited" });
        }

        // Quantize the cache key so small GPS jitter hits the same cache entry
        // (~11m at 4 decimal places near the equator). Nominatim TOS is also
        // happier when we don't fan out unique keys per millidegree.
        string cacheKey = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "places_reverse:{0:F4}:{1:F4}",
            latitude,
            longitude);
        RedisValue cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                var hit = JsonSerializer.Deserialize<PlaceCandidateDto>((string)cached!, _cacheJsonOptions);
                if (hit is not null)
                {
                    return Ok(hit);
                }
            }
            catch
            {
                // fall through to live fetch
            }
        }

        LocalitySummary? locality = await _localities.FindByPointAsync(latitude, longitude, ct);
        if (locality is null)
        {
            return NotFound(new { error = "No locality found for the given coordinates.", code = "locality_not_found" });
        }

        GeocodingResult? reverse;
        try
        {
            reverse = await _geocoding.ReverseGeocodeAsync(latitude, longitude, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Reverse geocode failed (coordinates redacted)");
            reverse = null;
        }

        PlaceCandidateDto result = new PlaceCandidateDto
        {
            DisplayName = TrimDisplayName(reverse?.DisplayName)
                ?? BuildFallbackLabel(locality),
            Latitude = latitude,
            Longitude = longitude,
            LocalityId = locality.Id,
            WardName = locality.WardName,
            CityName = locality.CityName,
        };

        await _redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(result, _cacheJsonOptions), ReverseCacheTtl);
        return Ok(result);
    }

    // Nominatim display_name is a long comma-joined string. Keep the first
    // two segments which give the area + city context. Mirrors the helper
    // in LocalitiesController; kept locally to avoid coupling two controllers
    // through a new shared utility for two call sites.
    private static string? TrimDisplayName(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        string[] parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? raw : string.Join(", ", parts.Take(2));
    }

    private static string BuildFallbackLabel(LocalitySummary locality)
    {
        if (!string.IsNullOrWhiteSpace(locality.CityName))
        {
            return $"{locality.WardName}, {locality.CityName}";
        }

        return locality.WardName;
    }
}

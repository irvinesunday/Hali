using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
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
            throw new ValidationException("Query must be at least 2 characters.", code: "places.query_too_short");
        }

        string query = q.Trim();
        if (query.Length > MaxSearchQueryLength)
        {
            throw new ValidationException($"Query must be at most {MaxSearchQueryLength} characters.", code: "places.query_too_long");
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string rateKey = $"ratelimit:places_search:{clientIp}";
        bool allowed = await _rateLimiter.IsAllowedAsync(rateKey, RateLimitMaxRequests, RateLimitWindow, ct);
        if (!allowed)
        {
            throw new RateLimitException(
                code: "places.search_rate_limited",
                message: "Too many requests.");
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
            throw new ValidationException("Invalid coordinates.", code: "places.invalid_coordinates");
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string rateKey = $"ratelimit:places_reverse:{clientIp}";
        bool allowed = await _rateLimiter.IsAllowedAsync(rateKey, RateLimitMaxRequests, RateLimitWindow, ct);
        if (!allowed)
        {
            throw new RateLimitException(
                code: "places.reverse_rate_limited",
                message: "Too many requests.");
        }

        // Spatial integrity: always resolve locality FRESH for the caller's
        // coordinates before consulting the label cache. The previous design
        // (cache the full PlaceCandidateDto keyed by {lat:F4}:{lng:F4})
        // allowed two callers whose points rounded into the same 4dp bucket
        // (~11m at the equator) to share a cached response — which, for a
        // ward-boundary bucket or a bucket that straddled the edge of known
        // coverage, could
        //   (a) return a neighbor's locality to a caller whose point was
        //       outside every Hali locality (bypass of the locality guard),
        //   (b) return a neighbor's wardName / cityName / localityId across
        //       a ward boundary, and
        //   (c) return a neighbor's lat/lng instead of the caller's own.
        // We now always call FindByPointAsync on the caller's coordinates
        // and only cache the reverse-geocoded label string (the expensive
        // Nominatim call). The response is rebuilt from the caller's
        // coordinates + the caller's fresh locality every time.
        LocalitySummary? locality = await _localities.FindByPointAsync(latitude, longitude, ct);
        if (locality is null)
        {
            throw new NotFoundException(code: "places.locality_not_found", message: "No locality found for the given coordinates.");
        }

        // Label-only cache. Keyed by quantized coords AND the resolved
        // localityId so a same-bucket cross-locality hit cannot serve a
        // neighbour's Nominatim label (which typically embeds the ward
        // name in its free-form string). Value is the already-trimmed
        // display name or null-sentinel when Nominatim returned nothing.
        string labelCacheKey = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "places_reverse_label:{0:F4}:{1:F4}:{2}",
            latitude,
            longitude,
            locality.Id);

        string? label = null;
        RedisValue cachedLabel = await _redis.StringGetAsync(labelCacheKey);
        if (cachedLabel.HasValue)
        {
            string raw = (string)cachedLabel!;
            if (!string.IsNullOrEmpty(raw))
            {
                label = raw;
            }
        }

        if (label is null)
        {
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

            label = TrimDisplayName(reverse?.DisplayName);
            if (!string.IsNullOrEmpty(label))
            {
                // Only cache when we have a real Nominatim label. Skipping
                // the write on a null/empty label avoids pinning a failed
                // lookup for the full TTL; the fallback label is cheap to
                // rebuild from the fresh locality each call.
                await _redis.StringSetAsync(labelCacheKey, label, ReverseCacheTtl);
            }
        }

        PlaceCandidateDto result = new PlaceCandidateDto
        {
            DisplayName = !string.IsNullOrEmpty(label) ? label : BuildFallbackLabel(locality),
            Latitude = latitude,
            Longitude = longitude,
            LocalityId = locality.Id,
            WardName = locality.WardName,
            CityName = locality.CityName,
        };

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

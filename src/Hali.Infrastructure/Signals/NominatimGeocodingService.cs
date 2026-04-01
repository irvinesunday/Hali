using System.Text.Json;
using Hali.Application.Signals;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Hali.Infrastructure.Signals;

public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _http;
    private readonly IDatabase _redis;
    private readonly ILogger<NominatimGeocodingService> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    public NominatimGeocodingService(
        HttpClient http,
        IDatabase redis,
        ILogger<NominatimGeocodingService> logger)
    {
        _http = http;
        _redis = redis;
        _logger = logger;
    }

    public async Task<GeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        var cacheKey = $"geocode:{latitude:F6}:{longitude:F6}";

        // Cache hit
        var cached = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                return JsonSerializer.Deserialize<GeocodingResult>((string)cached!);
            }
            catch
            {
                // Stale or corrupt cache — fall through to live call
            }
        }

        // Cache miss — call Nominatim
        var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude}&lon={longitude}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "Hali/1.0 (civic signal platform)");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(req, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nominatim request failed for {Lat},{Lng}", latitude, longitude);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Nominatim returned {Status} for {Lat},{Lng}", response.StatusCode, latitude, longitude);
            return null;
        }

        var rawBody = await response.Content.ReadAsStringAsync(ct);
        var result = ParseNominatimResponse(rawBody);

        if (result is not null)
        {
            var serialized = JsonSerializer.Serialize(result);
            await _redis.StringSetAsync(cacheKey, serialized, CacheTtl);
        }

        return result;
    }

    private GeocodingResult? ParseNominatimResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var displayName = root.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;

            string? road = null, suburb = null, city = null, country = null;

            if (root.TryGetProperty("address", out var addr))
            {
                road = GetAddressField(addr, "road", "pedestrian", "footway");
                suburb = GetAddressField(addr, "suburb", "neighbourhood", "village", "town");
                city = GetAddressField(addr, "city", "county", "state_district");
                country = GetAddressField(addr, "country");
            }

            return new GeocodingResult(displayName, road, suburb, city, country);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Nominatim response");
            return null;
        }
    }

    private static string? GetAddressField(JsonElement addr, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (addr.TryGetProperty(key, out var val) && val.ValueKind != JsonValueKind.Null)
                return val.GetString();
        }
        return null;
    }
}

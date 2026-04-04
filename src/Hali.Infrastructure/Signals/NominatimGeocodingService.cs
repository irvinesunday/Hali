using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

	public NominatimGeocodingService(HttpClient http, IDatabase redis, ILogger<NominatimGeocodingService> logger)
	{
		_http = http;
		_redis = redis;
		_logger = logger;
	}

	public async Task<GeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default(CancellationToken))
	{
		string cacheKey = $"geocode:{latitude:F6}:{longitude:F6}";
		RedisValue cached = await _redis.StringGetAsync(cacheKey);
		if (cached.HasValue)
		{
			try
			{
				return JsonSerializer.Deserialize<GeocodingResult>((string)cached!);
			}
			catch
			{
			}
		}
		using HttpRequestMessage req = new HttpRequestMessage(requestUri: $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={latitude}&lon={longitude}", method: HttpMethod.Get);
		req.Headers.Add("User-Agent", "Hali/1.0 (civic signal platform)");
		HttpResponseMessage response;
		try
		{
			response = await _http.SendAsync(req, ct);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_logger.LogWarning(ex2, "Nominatim request failed for {Lat},{Lng}", latitude, longitude);
			return null;
		}
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Nominatim returned {Status} for {Lat},{Lng}", response.StatusCode, latitude, longitude);
			return null;
		}
		GeocodingResult result = ParseNominatimResponse(await response.Content.ReadAsStringAsync(ct));
		if ((object)result != null)
		{
			string serialized = JsonSerializer.Serialize(result);
			await _redis.StringSetAsync(cacheKey, serialized, CacheTtl);
		}
		return result;
	}

	private GeocodingResult? ParseNominatimResponse(string json)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(json);
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement value;
			string displayName = (rootElement.TryGetProperty("display_name", out value) ? value.GetString() : null);
			string road = null;
			string suburb = null;
			string city = null;
			string country = null;
			if (rootElement.TryGetProperty("address", out var value2))
			{
				road = GetAddressField(value2, "road", "pedestrian", "footway");
				suburb = GetAddressField(value2, "suburb", "neighbourhood", "village", "town");
				city = GetAddressField(value2, "city", "county", "state_district");
				country = GetAddressField(value2, "country");
			}
			return new GeocodingResult(displayName, road, suburb, city, country);
		}
		catch (Exception exception)
		{
			_logger.LogWarning(exception, "Failed to parse Nominatim response");
			return null;
		}
	}

	private static string? GetAddressField(JsonElement addr, params string[] keys)
	{
		foreach (string propertyName in keys)
		{
			if (addr.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null)
			{
				return value.GetString();
			}
		}
		return null;
	}
}

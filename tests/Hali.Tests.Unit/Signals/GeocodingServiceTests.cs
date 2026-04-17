using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Application.Signals;
using Hali.Infrastructure.Signals;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Signals;

public class GeocodingServiceTests
{
	private static readonly string NominatimResponse = "{\n  \"display_name\": \"Lusaka Road, Nairobi West, Nairobi, Kenya\",\n  \"address\": {\n    \"road\": \"Lusaka Road\",\n    \"suburb\": \"Nairobi West\",\n    \"city\": \"Nairobi\",\n    \"country\": \"Kenya\"\n  }\n}";

	private static NominatimGeocodingService CreateService(HttpStatusCode status, string body, IDatabase? redis = null)
	{
		FakeHttpMessageHandler handler = new FakeHttpMessageHandler(status, body);
		HttpClient http = new HttpClient(handler);
		IDatabase redis2 = redis ?? CreateEmptyRedis();
		return new NominatimGeocodingService(http, redis2, NullLogger<NominatimGeocodingService>.Instance);
	}

	private static IDatabase CreateEmptyRedis()
	{
		IDatabase database = Substitute.For<IDatabase>(Array.Empty<object>());
		database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
		return database;
	}

	[Fact]
	public async Task ReverseGeocodeAsync_CacheMiss_CallsNominatimAndCaches()
	{
		FakeRedisDatabase fakeDb = new FakeRedisDatabase();
		NominatimGeocodingService svc = CreateService(HttpStatusCode.OK, NominatimResponse, fakeDb);
		GeocodingResult? result = await svc.ReverseGeocodeAsync(-1.3, 36.8);
		Assert.NotNull(result);
		Assert.Contains("Nairobi", result.DisplayName);
		Assert.Equal("Lusaka Road", result.Road);
		Assert.Equal("Nairobi West", result.Suburb);
		Assert.True(fakeDb.StringSetCalled, "Expected StringSetAsync to be called for geocode cache");
		Assert.StartsWith("geocode:", fakeDb.LastStringSetKey);
	}

	[Fact]
	public async Task ReverseGeocodeAsync_CacheHit_DoesNotCallNominatim()
	{
		GeocodingResult cachedResult = new GeocodingResult("Cached Display", "Cached Road", "Cached Suburb", "Nairobi", "Kenya");
		string cachedJson = JsonSerializer.Serialize(cachedResult);
		IDatabase db = Substitute.For<IDatabase>(Array.Empty<object>());
		db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(cachedJson);
		FakeHttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "should not be called");
		HttpClient http = new HttpClient(handler);
		NominatimGeocodingService svc = new NominatimGeocodingService(http, db, NullLogger<NominatimGeocodingService>.Instance);
		GeocodingResult? result = await svc.ReverseGeocodeAsync(-1.3, 36.8);
		Assert.NotNull(result);
		Assert.Equal("Cached Display", result.DisplayName);
		Assert.Equal("Cached Road", result.Road);
	}

	[Fact]
	public async Task ReverseGeocodeAsync_NominatimFails_ReturnsNull()
	{
		NominatimGeocodingService svc = CreateService(HttpStatusCode.ServiceUnavailable, "");
		Assert.Null(await svc.ReverseGeocodeAsync(-1.3, 36.8));
	}
}

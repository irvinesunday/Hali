using System.Net;
using System.Text.Json;
using Hali.Application.Signals;
using Hali.Infrastructure.Signals;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;

namespace Hali.Tests.Unit.Signals;

public class GeocodingServiceTests
{
    private static readonly string NominatimResponse = """
        {
          "display_name": "Lusaka Road, Nairobi West, Nairobi, Kenya",
          "address": {
            "road": "Lusaka Road",
            "suburb": "Nairobi West",
            "city": "Nairobi",
            "country": "Kenya"
          }
        }
        """;

    private static NominatimGeocodingService CreateService(
        HttpStatusCode status,
        string body,
        IDatabase? redis = null)
    {
        var handler = new FakeHttpMessageHandler(status, body);
        var http = new HttpClient(handler);
        var mockRedis = redis ?? CreateEmptyRedis();
        return new NominatimGeocodingService(http, mockRedis, NullLogger<NominatimGeocodingService>.Instance);
    }

    private static IDatabase CreateEmptyRedis()
    {
        var db = Substitute.For<IDatabase>();
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(RedisValue.Null);
        return db;
    }

    [Fact]
    public async Task ReverseGeocodeAsync_CacheMiss_CallsNominatimAndCaches()
    {
        var fakeDb = new FakeRedisDatabase();
        var svc = CreateService(HttpStatusCode.OK, NominatimResponse, fakeDb);

        var result = await svc.ReverseGeocodeAsync(-1.3, 36.8);

        Assert.NotNull(result);
        Assert.Contains("Nairobi", result.DisplayName);
        Assert.Equal("Lusaka Road", result.Road);
        Assert.Equal("Nairobi West", result.Suburb);

        // Verify cache was written
        Assert.True(fakeDb.StringSetCalled, "Expected StringSetAsync to be called for geocode cache");
        Assert.StartsWith("geocode:", fakeDb.LastStringSetKey);
    }

    [Fact]
    public async Task ReverseGeocodeAsync_CacheHit_DoesNotCallNominatim()
    {
        var cachedResult = new GeocodingResult("Cached Display", "Cached Road", "Cached Suburb", "Nairobi", "Kenya");
        var cachedJson = JsonSerializer.Serialize(cachedResult);

        var db = Substitute.For<IDatabase>();
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns((RedisValue)cachedJson);

        // Use a handler that would fail if called
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "should not be called");
        var http = new HttpClient(handler);
        var svc = new NominatimGeocodingService(http, db, NullLogger<NominatimGeocodingService>.Instance);

        var result = await svc.ReverseGeocodeAsync(-1.3, 36.8);

        Assert.NotNull(result);
        Assert.Equal("Cached Display", result.DisplayName);
        Assert.Equal("Cached Road", result.Road);
    }

    [Fact]
    public async Task ReverseGeocodeAsync_NominatimFails_ReturnsNull()
    {
        var svc = CreateService(HttpStatusCode.ServiceUnavailable, "");

        var result = await svc.ReverseGeocodeAsync(-1.3, 36.8);

        Assert.Null(result);
    }
}

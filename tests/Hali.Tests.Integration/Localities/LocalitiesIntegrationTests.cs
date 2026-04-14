using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Integration.Localities;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class LocalitiesIntegrationTests : IntegrationTestBase
{
    private const string WardListCacheKey = "locality_list:wards";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public LocalitiesIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    // ----------------------------------------------------------------------
    // GET /v1/localities/wards  — C10 searchable ward selection
    // ----------------------------------------------------------------------

    [Fact]
    public async Task ListWards_Anonymous_ReturnsSummariesSortedByWardName()
    {
        // Ensure a cold cache so we exercise the repository → Redis-SET path.
        await ClearWardListCacheAsync();

        var response = await Client.GetAsync("/v1/localities/wards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(
            body.GetArrayLength() >= 2,
            "Expected at least two seeded test wards so sort order is exercised.");

        string? previousWardName = null;
        foreach (var ward in body.EnumerateArray())
        {
            Assert.True(ward.TryGetProperty("localityId", out _));
            Assert.True(ward.TryGetProperty("wardName", out var wardNameProp));
            var wardName = wardNameProp.GetString();
            Assert.False(string.IsNullOrWhiteSpace(wardName));

            // cityName must be present on the wire (nullable but always serialized).
            Assert.True(ward.TryGetProperty("cityName", out _));

            // No leaked fields from the domain record / geocoding-search DTO.
            Assert.False(ward.TryGetProperty("countyName", out _));
            Assert.False(ward.TryGetProperty("placeLabel", out _));

            if (previousWardName is not null)
            {
                Assert.True(
                    string.Compare(previousWardName, wardName, StringComparison.Ordinal) <= 0,
                    $"Expected wards sorted by wardName ascending, but '{previousWardName}' preceded '{wardName}'.");
            }
            previousWardName = wardName;
        }
    }

    [Fact]
    public async Task ListWards_SecondCall_IsServedFromRedisCache()
    {
        await ClearWardListCacheAsync();

        // Cold — populates cache
        var cold = await Client.GetAsync("/v1/localities/wards");
        Assert.Equal(HttpStatusCode.OK, cold.StatusCode);

        // The cache entry must exist after the cold call.
        await using var scope = Factory.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var cached = await redis.StringGetAsync(WardListCacheKey);
        Assert.True(cached.HasValue, "Expected ward list to be cached in Redis after first request.");

        // Overwrite the cache entry with a sentinel payload that cannot be
        // produced by the repository. If the second response returns this
        // sentinel, we've proven the controller served from cache rather
        // than recomputing from the repository.
        //
        // The controller caches via default-options JsonSerializer (PascalCase)
        // and the ASP.NET Core output pipeline writes the wire response in
        // camelCase — so we write the sentinel in PascalCase to match the
        // deserialize side, and assert on the camelCase wire shape.
        const string sentinelBody =
            "[{\"LocalityId\":\"00000000-0000-0000-0000-000000000001\",\"WardName\":\"__cache_sentinel__\",\"CityName\":null}]";
        await redis.StringSetAsync(WardListCacheKey, sentinelBody);

        var warm = await Client.GetAsync("/v1/localities/wards");
        Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        var warmBody = await warm.Content.ReadAsStringAsync();

        // JSON equivalence: the controller round-trips cache payload through
        // List<LocalitySummaryDto> then back through the framework's JSON
        // writer, so exact byte equality is not guaranteed. Compare on the
        // sentinel ward-name marker instead.
        using var doc = JsonDocument.Parse(warmBody);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal(
            "__cache_sentinel__",
            doc.RootElement[0].GetProperty("wardName").GetString());
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private async Task ClearWardListCacheAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IDatabase>();
        await redis.KeyDeleteAsync(WardListCacheKey);
    }
}

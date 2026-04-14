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
        // Post-#127: the controller caches via MVC-configured JsonSerializer
        // options (camelCase) so the cache and wire contracts match. The
        // sentinel is written in camelCase for the same reason, and the
        // assertion below verifies the wire response preserves the sentinel
        // ward name.
        const string sentinelBody =
            "[{\"localityId\":\"00000000-0000-0000-0000-000000000001\",\"wardName\":\"__cache_sentinel__\",\"cityName\":null}]";
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

    /// <summary>
    /// Regression lock for Issue #127. The Redis cache payload MUST use the
    /// same camelCase property naming as the HTTP wire response — any future
    /// drift back to default-options <see cref="JsonSerializer"/> would
    /// produce PascalCase cache entries while the ASP.NET Core output
    /// pipeline continues to write camelCase, silently breaking clients on
    /// cache hits that pass through without reserialization.
    /// </summary>
    [Fact]
    public async Task ListWards_CachedPayload_UsesCamelCasePropertyNames()
    {
        await ClearWardListCacheAsync();

        // Cold — populates cache via the repository → serializer path.
        var response = await Client.GetAsync("/v1/localities/wards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var raw = (string?)await redis.StringGetAsync(WardListCacheKey);

        Assert.False(
            string.IsNullOrEmpty(raw),
            "Expected ward list to be cached in Redis after the cold call.");

        // Casing invariant: the cache must carry the camelCase keys the
        // client consumes over the wire. Assert both the presence of the
        // camelCase keys AND the absence of the PascalCase ones.
        Assert.Contains("\"localityId\"", raw);
        Assert.Contains("\"wardName\"", raw);
        Assert.Contains("\"cityName\"", raw);
        Assert.DoesNotContain("\"LocalityId\"", raw);
        Assert.DoesNotContain("\"WardName\"", raw);
        Assert.DoesNotContain("\"CityName\"", raw);
    }

    // ----------------------------------------------------------------------
    // GET /v1/localities/search — cache JSON alignment (Issue #127)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Rollout compatibility for Issue #127. Any Redis entry written before
    /// the fix uses PascalCase property names (default-options
    /// <see cref="JsonSerializer"/>). The fix uses MVC-configured options
    /// whose <c>PropertyNameCaseInsensitive = true</c> handles those legacy
    /// entries cleanly, so no cache-key bump or manual invalidation is
    /// needed during deploy. This test seeds a PascalCase sentinel and
    /// asserts the wire response comes back as camelCase (i.e. the
    /// deserialize + Ok() reserialize path works end to end).
    /// </summary>
    [Fact]
    public async Task Search_LegacyPascalCaseCacheEntry_IsServedAsCamelCase()
    {
        const string query = "jsonoptions-rollout-probe";
        var cacheKey = $"locality_search:{query}";

        await using var scope = Factory.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IDatabase>();

        // Pre-seed a PascalCase entry — the exact shape the OLD controller
        // would have written before this fix.
        const string legacyPascalCaseEntry =
            "[{\"LocalityId\":\"00000000-0000-0000-0000-000000000002\"," +
            "\"PlaceLabel\":\"Legacy Place\"," +
            "\"WardName\":\"Legacy Ward\"," +
            "\"CityName\":\"Nairobi\"}]";
        await redis.StringSetAsync(cacheKey, legacyPascalCaseEntry);

        var response = await Client.GetAsync($"/v1/localities/search?q={query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(1, doc.RootElement.GetArrayLength());

        var first = doc.RootElement[0];
        // Wire must be camelCase regardless of the cached entry's casing.
        Assert.Equal("Legacy Ward", first.GetProperty("wardName").GetString());
        Assert.Equal("Legacy Place", first.GetProperty("placeLabel").GetString());
        Assert.Equal("Nairobi", first.GetProperty("cityName").GetString());
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

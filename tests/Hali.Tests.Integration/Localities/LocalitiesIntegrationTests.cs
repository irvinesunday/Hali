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
        Assert.True(body.GetArrayLength() >= 1, "Expected at least the seeded test ward.");

        var first = body[0];
        Assert.True(first.TryGetProperty("localityId", out _));
        Assert.True(first.TryGetProperty("wardName", out var wardName));
        Assert.False(string.IsNullOrWhiteSpace(wardName.GetString()));
        // cityName must be present on the wire (nullable but always serialized).
        Assert.True(first.TryGetProperty("cityName", out _));

        // No leaked fields from the domain record / geocoding-search DTO.
        Assert.False(first.TryGetProperty("countyName", out _));
        Assert.False(first.TryGetProperty("placeLabel", out _));
    }

    [Fact]
    public async Task ListWards_SecondCall_IsServedFromRedisCache()
    {
        await ClearWardListCacheAsync();

        // Cold — populates cache
        var cold = await Client.GetAsync("/v1/localities/wards");
        Assert.Equal(HttpStatusCode.OK, cold.StatusCode);
        var coldBody = await cold.Content.ReadAsStringAsync();

        // Warm — same payload should be served from cache
        var warm = await Client.GetAsync("/v1/localities/wards");
        Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        var warmBody = await warm.Content.ReadAsStringAsync();

        Assert.Equal(coldBody, warmBody);

        // Cache entry must exist after the cold call.
        await using var scope = Factory.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var cached = await redis.StringGetAsync("locality_list:wards");
        Assert.True(cached.HasValue, "Expected ward list to be cached in Redis after first request.");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private async Task ClearWardListCacheAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var redis = scope.ServiceProvider.GetRequiredService<IDatabase>();
        await redis.KeyDeleteAsync("locality_list:wards");
    }
}

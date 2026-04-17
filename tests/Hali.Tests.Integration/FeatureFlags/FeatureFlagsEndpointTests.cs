using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Hali.Tests.Integration.FeatureFlags;

/// <summary>
/// Integration tests for the client-safe feature-flag exposure endpoint
/// introduced in #194. The endpoint returns the resolved value for every
/// <c>ClientVisible</c> flag and never leaks <c>ServerOnly</c> flags. It
/// supports anonymous callers (contract: <c>actor_type = "anonymous"</c>
/// when no JWT is present).
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FeatureFlagsEndpointTests : IntegrationTestBase
{
    public FeatureFlagsEndpointTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task GetFeatureFlags_Anonymous_Returns200WithFlagsMap()
    {
        HttpResponseMessage response = await Client.GetAsync("/v1/feature-flags");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("flags", out JsonElement flags));
        Assert.Equal(JsonValueKind.Object, flags.ValueKind);
    }

    [Fact]
    public async Task GetFeatureFlags_IncludesClientVisibleFlag()
    {
        HttpResponseMessage response = await Client.GetAsync("/v1/feature-flags");
        response.EnsureSuccessStatusCode();

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement flags = body.RootElement.GetProperty("flags");

        // MobileHomeConditionBadgeEnabled is a client-visible flag in the
        // catalog and must be present in the response. Asserting on the
        // resolved boolean keeps the test honest about the contract
        // (value is a JSON boolean) without tying the test to a specific
        // environment's targeting outcome, which could legitimately flip.
        Assert.True(flags.TryGetProperty("mobile.home.condition_badge.enabled", out JsonElement resolved));
        Assert.True(
            resolved.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Expected the client-visible flag to resolve to a JSON boolean value.");
    }

    [Fact]
    public async Task GetFeatureFlags_DoesNotLeakServerOnlyFlag()
    {
        HttpResponseMessage response = await Client.GetAsync("/v1/feature-flags");
        response.EnsureSuccessStatusCode();

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement flags = body.RootElement.GetProperty("flags");

        // WorkersPushDispatcherEnabled is server-only and must NEVER be
        // returned — even if it defaults to true.
        Assert.False(flags.TryGetProperty("workers.push_dispatcher.enabled", out _));
    }

    [Fact]
    public async Task GetFeatureFlags_AuthenticatedCaller_ReceivesResolvedFlags()
    {
        string token = MintJwt(accountId: Guid.NewGuid(), role: "citizen");

        HttpRequestMessage request = new(HttpMethod.Get, "/v1/feature-flags");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement flags = body.RootElement.GetProperty("flags");
        Assert.True(flags.TryGetProperty("mobile.home.condition_badge.enabled", out _));
    }

    [Fact]
    public async Task GetFeatureFlags_WithLocalityIdQuery_Returns200()
    {
        // Locality id is accepted but no catalog flag currently targets it;
        // the response shape must still be valid.
        HttpResponseMessage response = await Client.GetAsync(
            $"/v1/feature-flags?localityId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("flags", out _));
    }

    [Fact]
    public async Task GetFeatureFlags_InvalidLocalityId_Returns400()
    {
        HttpResponseMessage response = await Client.GetAsync("/v1/feature-flags?localityId=not-a-guid");

        // ASP.NET Core model binding rejects a non-Guid value with 400.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string MintJwt(Guid accountId, string role)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(ClaimTypes.Role, role),
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

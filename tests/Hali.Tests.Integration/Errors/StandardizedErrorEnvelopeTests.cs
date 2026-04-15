using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Integration.Errors;

/// <summary>
/// Cross-cutting integration coverage for the H2 refactor: proves that the
/// previously-seam-specific ad-hoc error payloads now land on the canonical
/// ApiErrorResponse envelope emitted by the H1 exception middleware.
///
/// One representative path per retyped seam is asserted here — enough to
/// catch regressions where a controller reintroduces a local try/catch or
/// an ad-hoc {error: "..."} shape without re-breaking every pre-existing
/// endpoint-specific test.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class StandardizedErrorEnvelopeTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public StandardizedErrorEnvelopeTests(HaliWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task VerifyOtp_WrongCode_EmitsStandardErrorEnvelope()
    {
        const string phone = "+254711900001";
        await SeedOtpAsync(phone, "111111");

        var response = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination = phone,
            otp = "000000",
            deviceFingerprintHash = "h2-env-verify-wrong",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "auth.otp_invalid");
    }

    [Fact]
    public async Task Refresh_InvalidToken_EmitsStandardErrorEnvelope()
    {
        var response = await Client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            refreshToken = "definitely-not-a-real-refresh-token",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "auth.refresh_token_invalid");
    }

    [Fact]
    public async Task LocalitySearch_QueryTooShort_EmitsStandardErrorEnvelope()
    {
        var response = await Client.GetAsync("/v1/localities/search?q=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "locality.query_too_short");
    }

    [Fact]
    public async Task ResolveCoordinates_OutOfRange_EmitsStandardErrorEnvelope()
    {
        // H3 (#153): this path previously emitted the concept-collision code
        // `locality.invalid_coordinates`. It is now the canonical
        // `validation.invalid_coordinates` (merged with the parallel
        // `places.invalid_coordinates` site).
        var response = await Client.GetAsync("/v1/localities/resolve-by-coordinates?latitude=999&longitude=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "validation.invalid_coordinates");
    }

    [Fact]
    public async Task PlacesReverse_OutOfRange_EmitsValidationInvalidCoordinates()
    {
        // H3 (#153): proves the `places.invalid_coordinates` rename to the
        // canonical `validation.invalid_coordinates` (the same wire code now
        // returned by `/v1/localities/resolve-by-coordinates` for out-of-range
        // input). Asserts the collision was truly collapsed on the wire.
        var response = await Client.GetAsync("/v1/places/reverse?latitude=999&longitude=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "validation.invalid_coordinates");
    }

    [Fact]
    public async Task PlacesSearch_QueryTooShort_EmitsStandardErrorEnvelope()
    {
        var response = await Client.GetAsync("/v1/places/search?q=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "places.query_too_short");
    }

    [Fact]
    public async Task ClusterParticipation_MissingDeviceHash_EmitsValidationMissingField()
    {
        // H3 (#153): proves the `validation.failed` -> `validation.missing_field`
        // rename for discriminable missing-required-field cases. The cluster-
        // participation endpoint validates the request body before looking up
        // the cluster, so a random cluster id is sufficient — the validation
        // path is the only thing exercised.
        var jwt = MintJwt(Guid.NewGuid());
        using var authed = CreateAuthenticatedClient(jwt);

        var response = await authed.PostAsJsonAsync(
            $"/v1/clusters/{Guid.NewGuid()}/participation",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "validation.missing_field");
    }

    [Fact]
    public async Task SignalsPreview_RateLimited_EmitsRateLimitExceeded()
    {
        // H3 (#153): proves the `integrity.rate_limited` -> `rate_limit.exceeded`
        // rename. The preview endpoint caps anonymous callers at 10/IP/10min;
        // the 11th call is the first to cross the threshold.
        //
        // Redis state persists across tests in this collection, so clear any
        // residual preview-rate-limit counter before counting — otherwise the
        // outcome depends on the order xunit happens to pick.
        await ClearPreviewRateLimitKeysAsync();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await Client.PostAsJsonAsync("/v1/signals/preview", new
            {
                freeText = $"rate-limit-probe-{i} there is no water in my area",
            });
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        AssertErrorEnvelope(await last.Content.ReadFromJsonAsync<JsonElement>(_json), "rate_limit.exceeded");
    }

    private async Task ClearPreviewRateLimitKeysAsync()
    {
        // Under the WebApplicationFactory TestServer the remote IP is
        // unset, so SignalsController.Preview builds the key as
        // `rl:signal-preview:unknown`. Deleting that single key is
        // sufficient and avoids requiring `allowAdmin` on the Redis
        // connection just to enumerate matching keys.
        using var scope = Factory.Services.CreateScope();
        var mux = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        await mux.GetDatabase().KeyDeleteAsync("rl:signal-preview:unknown");
    }

    [Fact]
    public async Task UsersMe_AccountMissing_EmitsStandardErrorEnvelope()
    {
        // Exercises the H2 retype of UsersController's bare NotFound() -> typed
        // NotFoundException("account.not_found"). A framework JWT challenge on
        // a missing Authorization header returns 401 before the controller
        // runs, so we cannot exercise the application-layer envelope path by
        // omitting the header; instead we mint a valid JWT bound to a random
        // account ID that has never been persisted, which routes the request
        // through the controller and into its typed-exception branch.
        var jwt = MintJwt(Guid.NewGuid());

        using var authedClient = CreateAuthenticatedClient(jwt);
        var response = await authedClient.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorEnvelope(await response.Content.ReadFromJsonAsync<JsonElement>(_json), "account.not_found");
    }

    private static string MintJwt(Guid accountId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task OtpRequested_Success_EmitsTypedSuccessDto()
    {
        // Proves the Commit 5 DTO-ification preserves the body shape the mobile
        // client consumes: {"message":"OTP sent"} on 200 OK.
        var response = await Client.PostAsJsonAsync("/v1/auth/otp", new
        {
            destination = "+254711900002",
            authMethod = "phone_otp",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("OTP sent", body.GetProperty("message").GetString());
    }

    /// <summary>
    /// Every H2-touched error path must land on the canonical envelope:
    /// { error: { code, message, traceId, details? } }.
    /// </summary>
    private static void AssertErrorEnvelope(JsonElement body, string expectedCode)
    {
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        Assert.True(body.TryGetProperty("error", out var error),
            "response body must have a top-level `error` property");
        Assert.Equal(JsonValueKind.Object, error.ValueKind);

        Assert.True(error.TryGetProperty("code", out var code));
        Assert.Equal(expectedCode, code.GetString());

        Assert.True(error.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));

        Assert.True(error.TryGetProperty("traceId", out _),
            "error envelope must include a traceId for support/log correlation");
    }
}

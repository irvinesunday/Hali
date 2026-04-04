using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Xunit;

namespace Hali.Tests.Integration.Auth;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class AuthIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AuthIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    // -----------------------------------------------------------------------
    // RequestOtp
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RequestOtp_ValidPhone_Returns200()
    {
        // Note: field names use camelCase to match the API's JSON naming policy
        var response = await Client.PostAsJsonAsync("/v1/auth/otp", new
        {
            destination = "+254711000001",
            authMethod  = "phone_otp",   // controller normalises snake → Pascal
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("OTP sent", body.GetProperty("message").GetString());
    }

    // -----------------------------------------------------------------------
    // VerifyOtp
    // -----------------------------------------------------------------------

    [Fact]
    public async Task VerifyOtp_ValidCode_ReturnsTokenPair()
    {
        const string phone = "+254711000002";
        const string otp   = "123456";
        await SeedOtpAsync(phone, otp);

        var response = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination             = phone,
            otp                     = otp,
            deviceFingerprintHash   = "test-verify-device-001",
            platform                = "ios",
            appVersion              = "1.0.0",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_Returns400()
    {
        const string phone = "+254711000003";
        await SeedOtpAsync(phone, "888888");

        var response = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination           = phone,
            otp                   = "000000",   // wrong
            deviceFingerprintHash = "test-verify-device-002",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // RefreshToken
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RefreshToken_ValidToken_ReturnsNewPair()
    {
        const string phone = "+254711000004";
        const string otp   = "234567";
        await SeedOtpAsync(phone, otp);

        var verifyResp = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination           = phone,
            otp                   = otp,
            deviceFingerprintHash = "test-refresh-device-001",
        });
        verifyResp.EnsureSuccessStatusCode();

        var tokens       = await verifyResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var refreshToken = tokens.GetProperty("refreshToken").GetString()!;

        var refreshResp = await Client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            refreshToken = refreshToken,
        });

        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var newTokens = await refreshResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.False(string.IsNullOrWhiteSpace(newTokens.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(newTokens.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task RefreshToken_AfterRotation_OldTokenRejected()
    {
        const string phone = "+254711000005";
        const string otp   = "345678";
        await SeedOtpAsync(phone, otp);

        var verifyResp = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination           = phone,
            otp                   = otp,
            deviceFingerprintHash = "test-refresh-device-002",
        });
        verifyResp.EnsureSuccessStatusCode();

        var tokens          = await verifyResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var originalRefresh = tokens.GetProperty("refreshToken").GetString()!;

        // Consume the refresh token (rotation)
        var firstRefresh = await Client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            refreshToken = originalRefresh,
        });
        firstRefresh.EnsureSuccessStatusCode();

        // Re-using the original refresh token must now fail
        var secondRefresh = await Client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            refreshToken = originalRefresh,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Logout
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Logout_ValidToken_RevokesToken()
    {
        const string phone = "+254711000006";
        const string otp   = "456789";
        await SeedOtpAsync(phone, otp);

        var verifyResp = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination           = phone,
            otp                   = otp,
            deviceFingerprintHash = "test-logout-device-001",
        });
        verifyResp.EnsureSuccessStatusCode();

        var tokens       = await verifyResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var refreshToken = tokens.GetProperty("refreshToken").GetString()!;

        // Logout
        var logoutResp = await Client.PostAsJsonAsync("/v1/auth/logout", new
        {
            refreshToken = refreshToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        // Token should now be revoked
        var refreshResp = await Client.PostAsJsonAsync("/v1/auth/refresh", new
        {
            refreshToken = refreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }
}

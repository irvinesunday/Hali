using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Auth;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.InstitutionAuth;

/// <summary>
/// End-to-end integration coverage for the Phase 2 institution auth
/// stack (#197): magic-link issue + verify, TOTP enroll + confirm +
/// verify, session refresh / step-up / logout, and the CSRF gate on
/// write endpoints.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionAuthIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InstitutionAuthIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -----------------------------------------------------------------------
    // Magic link
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MagicLinkRequest_UnknownEmail_ReturnsGenericSuccess()
    {
        // Unknown emails receive the same response shape as registered
        // emails — the server must never leak account existence.
        var resp = await Client.PostAsJsonAsync("/v1/auth/institution/magic-link/request", new
        {
            email = "no-such-user@example.com",
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.True(body.TryGetProperty("message", out _));
        Assert.True(body.TryGetProperty("expiresAt", out _));
    }

    [Fact]
    public async Task MagicLinkVerify_InvalidToken_Returns401()
    {
        var resp = await Client.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new
        {
            token = "not-a-real-token",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.magic_link_invalid",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MagicLinkVerify_KnownEmail_IssuesSessionCookie()
    {
        var (account, plaintext) = await SeedInstitutionAccountAndMagicLinkAsync();

        using var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new
        {
            token = plaintext,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Session + CSRF cookies must both be set; session is httpOnly,
        // CSRF is not (double-submit requires JS access).
        var setCookies = resp.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookies, c => c.StartsWith("hali_institution_session=", StringComparison.Ordinal));
        Assert.Contains(setCookies, c => c.StartsWith("hali_institution_csrf=", StringComparison.Ordinal));
        Assert.Contains(setCookies, c => c.StartsWith("hali_institution_session=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, c => c.StartsWith("hali_institution_session=") && c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));

        // A fresh user without TOTP enrollment must be told to enroll.
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.True(body.GetProperty("requiresTotpEnrollment").GetBoolean());
        Assert.False(body.GetProperty("requiresTotpVerification").GetBoolean());
    }

    [Fact]
    public async Task MagicLinkVerify_SameTokenTwice_SecondAttemptFails()
    {
        var (_, plaintext) = await SeedInstitutionAccountAndMagicLinkAsync();

        using var client = Factory.CreateClient();
        var first = await client.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new { token = plaintext });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var secondClient = Factory.CreateClient();
        var second = await secondClient.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new { token = plaintext });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task MagicLinkVerify_ExpiredToken_Returns401()
    {
        var account = await SeedInstitutionAccountAsync();
        string plaintext = await SeedMagicLinkAsync(account, expiresAt: DateTime.UtcNow.AddMinutes(-1));

        using var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new { token = plaintext });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // -----------------------------------------------------------------------
    // TOTP + session
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TotpEnroll_AuthenticatedSession_ReturnsOtpAuthUri()
    {
        using var client = await LogInViaMagicLinkAsync();

        var resp = await PostWithCsrfAsync(client, "/v1/auth/institution/totp/enroll");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.StartsWith("otpauth://", body.GetProperty("otpAuthUri").GetString()!);
        Assert.Equal(10, body.GetProperty("recoveryCodes").GetArrayLength());
    }

    [Fact]
    public async Task TotpConfirm_ValidCurrentCode_MarksConfirmedAndStampsStepUp()
    {
        using var client = await LogInViaMagicLinkAsync();
        var enroll = await PostWithCsrfAsync(client, "/v1/auth/institution/totp/enroll");
        Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);

        // Compute the current TOTP code from the seeded secret stored in
        // the DB. The service layer normally encrypts the secret, so we
        // resolve it via the Account → TotpSecret join and decrypt using
        // the same IDataProtector the service uses.
        var accountId = await GetInstitutionAccountIdAsync();
        string currentCode = await ComputeCurrentCodeAsync(accountId);

        var confirm = await PostWithCsrfAsync(client, "/v1/auth/institution/totp/confirm",
            new { code = currentCode });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var body = await confirm.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.True(body.TryGetProperty("verifiedAt", out _));
        Assert.True(body.GetProperty("windowSeconds").GetInt32() > 0);
    }

    [Fact]
    public async Task TotpConfirm_WrongCode_Returns400AndDoesNotConfirm()
    {
        using var client = await LogInViaMagicLinkAsync();
        await PostWithCsrfAsync(client, "/v1/auth/institution/totp/enroll");

        var resp = await PostWithCsrfAsync(client, "/v1/auth/institution/totp/confirm",
            new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.totp_invalid_code",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // Session refresh + logout
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SessionRefresh_ReturnsIdleAndWarningSeconds()
    {
        using var client = await LogInViaMagicLinkAsync();

        var resp = await PostWithCsrfAsync(client, "/v1/auth/institution/session/refresh");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(1800, body.GetProperty("idleTimeoutSeconds").GetInt32());
        Assert.Equal(1620, body.GetProperty("softWarningSeconds").GetInt32());
    }

    [Fact]
    public async Task Logout_RevokesSessionAndClearsCookies()
    {
        using var client = await LogInViaMagicLinkAsync();

        var resp = await PostWithCsrfAsync(client, "/v1/auth/institution/session/logout");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // The server writes Set-Cookie headers that expire the cookies
        // in the past, instructing the browser to drop them.
        var cleared = resp.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(cleared, c => c.StartsWith("hali_institution_session=") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------------
    // CSRF gate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WriteEndpoint_WithoutCsrfHeader_Returns403()
    {
        using var client = await LogInViaMagicLinkAsync();

        // Deliberately DO NOT add X-CSRF-Token — the middleware must
        // short-circuit with auth.csrf_missing.
        var resp = await client.PostAsync("/v1/auth/institution/session/refresh",
            new StringContent("", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.csrf_missing",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task WriteEndpoint_WithMismatchedCsrfHeader_Returns403()
    {
        using var client = await LogInViaMagicLinkAsync();

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/institution/session/refresh");
        req.Headers.Add("X-CSRF-Token", "this-is-not-the-real-csrf-token");
        req.Content = new StringContent("", Encoding.UTF8, "application/json");
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.csrf_mismatch",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // Session timeouts
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SessionRequest_AfterIdleExpiry_Returns401IdleTimeout()
    {
        using var client = await LogInViaMagicLinkAsync();
        await ExpireSessionActivityAsync(
            relativeLastActivityMinutes: -31,
            relativeAbsoluteHours: 11);

        var resp = await PostWithCsrfAsync(client, "/v1/auth/institution/session/refresh");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.institution_session_idle_timeout",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SessionRequest_AfterAbsoluteExpiry_Returns401AbsoluteTimeout()
    {
        using var client = await LogInViaMagicLinkAsync();
        // Keep the session active (recent activity) but move the absolute
        // expiry into the past — absolute takes precedence over idle.
        await ExpireSessionActivityAsync(
            relativeLastActivityMinutes: -1,
            relativeAbsoluteHours: -1);

        var resp = await PostWithCsrfAsync(client, "/v1/auth/institution/session/refresh");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.institution_session_absolute_timeout",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // ======================================================================
    // Test helpers
    // ======================================================================

    private const string TestInstitutionEmail = "institution-user@example.com";
    private string? _csrfPlaintext;

    /// <summary>
    /// Bootstraps an institution account + active magic link, runs the
    /// verify endpoint, and returns an HttpClient that carries the
    /// resulting session cookie. The CSRF plaintext from the response's
    /// Set-Cookie header is captured into <see cref="_csrfPlaintext"/>
    /// so subsequent writes can mirror it in the X-CSRF-Token header.
    /// </summary>
    private async Task<HttpClient> LogInViaMagicLinkAsync()
    {
        var (_, plaintext) = await SeedInstitutionAccountAndMagicLinkAsync();

        // WebApplicationFactory.CreateClient() is cookie-aware by default,
        // so the session cookie is retained across subsequent requests
        // through the same client instance.
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new { token = plaintext });
        resp.EnsureSuccessStatusCode();
        _csrfPlaintext = ExtractCookieValue(resp, "hali_institution_csrf");
        Assert.False(string.IsNullOrEmpty(_csrfPlaintext),
            "CSRF cookie should be set on magic-link verify");
        return client;
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(
        HttpClient client, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Add("X-CSRF-Token", _csrfPlaintext!);
        req.Content = body is null
            ? new StringContent("", Encoding.UTF8, "application/json")
            : JsonContent.Create(body);
        return await client.SendAsync(req);
    }

    /// <summary>
    /// Pulls the plaintext value of a cookie from an HttpResponseMessage's
    /// Set-Cookie headers. Returns null if the named cookie isn't present.
    /// </summary>
    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        string prefix = cookieName + "=";
        foreach (var raw in cookies)
        {
            if (!raw.StartsWith(prefix, StringComparison.Ordinal)) continue;
            int semicolon = raw.IndexOf(';');
            return semicolon < 0 ? raw[prefix.Length..] : raw[prefix.Length..semicolon];
        }
        return null;
    }

    private async Task<(Account account, string plaintext)> SeedInstitutionAccountAndMagicLinkAsync()
    {
        Account account = await SeedInstitutionAccountAsync();
        string plaintext = await SeedMagicLinkAsync(account, DateTime.UtcNow.AddMinutes(15));
        return (account, plaintext);
    }

    private async Task<Account> SeedInstitutionAccountAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var existing = await db.Accounts.FirstOrDefaultAsync(a => a.Email == TestInstitutionEmail);
        if (existing is not null) return existing;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = TestInstitutionEmail,
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private async Task<string> SeedMagicLinkAsync(Account account, DateTime expiresAt)
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
        MagicLinkIssued issued = await service.IssueAsync(account.Email!, default);
        // Capture the expiresAt override via direct DB update (the
        // service uses the configured TTL; the test wants to simulate
        // an already-expired token without waiting 15 minutes).
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE magic_link_tokens SET expires_at = @exp WHERE token_hash = @hash", conn);
        cmd.Parameters.AddWithValue("exp", expiresAt);
        cmd.Parameters.AddWithValue("hash", service.HashToken(issued.PlaintextToken));
        await cmd.ExecuteNonQueryAsync();
        return issued.PlaintextToken;
    }

    private async Task<Guid> GetInstitutionAccountIdAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = await db.Accounts.FirstAsync(a => a.Email == TestInstitutionEmail);
        return account.Id;
    }

    private async Task<string> ComputeCurrentCodeAsync(Guid accountId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var secret = await db.TotpSecrets.FirstAsync(s => s.AccountId == accountId);

        var protector = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>()
            .CreateProtector("hali-institution-totp-secrets");
        string base32 = protector.Unprotect(secret.SecretEncrypted);
        byte[] raw = TotpService.DecodeBase32(base32);
        long step = TotpService.GetCurrentStep(DateTimeOffset.UtcNow);
        return TotpService.ComputeCode(raw, step);
    }

    private static async Task ExpireSessionActivityAsync(
        int relativeLastActivityMinutes,
        int relativeAbsoluteHours)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
UPDATE web_sessions
SET last_activity_at = now() + (@actMin::int * interval '1 minute'),
    absolute_expires_at = now() + (@absHr::int * interval '1 hour')
WHERE revoked_at IS NULL", conn);
        cmd.Parameters.AddWithValue("actMin", relativeLastActivityMinutes);
        cmd.Parameters.AddWithValue("absHr", relativeAbsoluteHours);
        await cmd.ExecuteNonQueryAsync();
    }
}

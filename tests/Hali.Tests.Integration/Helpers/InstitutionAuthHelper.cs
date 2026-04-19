using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Helpers;

/// <summary>
/// Shared auth helper for Phase 2 integration tests (#241).
///
/// Phase 2 test classes previously minted JWTs directly, duplicating the
/// same claim-shape across multiple files and bypassing the institution
/// session middleware entirely. This helper centralises both auth paths
/// institution surfaces actually serve in production:
///
///   * <see cref="CreateSessionAsync"/> — drives the REAL magic-link
///     (+ optional TOTP step-up) flow end-to-end and returns a cookie-
///     bearing HttpClient. Use for tests that exercise institution /
///     institution_admin behaviour under the cookie session surface.
///
///   * <see cref="CreateBearerClient"/> — mints a JWT against
///     <see cref="Infrastructure.TestConstants"/>. The bearer path is
///     a real auth path: <c>InstitutionSessionMiddleware</c>
///     short-circuits requests that carry a Bearer header, falling
///     through to JwtBearer validation. Use for tests that explicitly
///     assert bearer-flow behaviour (e.g. "bearer JWT cannot satisfy
///     step-up", citizen-role 403 checks).
///
/// Every account / institution created by the helper uses UUID-suffixed
/// identifiers so concurrent test runs cannot collide on unique-email
/// constraints. Cleanup relies on the existing <c>CleanTablesAsync</c>
/// TRUNCATE pattern — the helper writes no state that survives a test.
/// </summary>
public static class InstitutionAuthHelper
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    // Cookie names match InstitutionAuthOptions defaults — kept as
    // constants here so callers (and this helper) never have to guess.
    public const string SessionCookieName = "hali_institution_session";
    public const string CsrfCookieName = "hali_institution_csrf";

    /// <summary>
    /// Authenticates an institution user via the REAL magic-link flow
    /// and returns a session-bearing <see cref="InstitutionAuthSession"/>.
    ///
    /// Flow:
    ///   1. Ensure an institution exists (create a fresh one if not given).
    ///   2. Create an institution account with the requested role flag,
    ///      using a unique email so parallel runs cannot collide.
    ///   3. Issue a magic link via <see cref="IMagicLinkService"/> and
    ///      bind it to the account id (same deterministic-tie pattern the
    ///      existing tests use — MagicLinkService.IssueAsync does the
    ///      email→account join itself, but this helper re-asserts it
    ///      defensively so the test is independent of the lookup order).
    ///   4. POST /v1/auth/institution/magic-link/verify to receive the
    ///      session + CSRF cookies. The cookie-aware HttpClient retains
    ///      them across subsequent requests.
    ///   5. Optionally drive TOTP enroll + confirm so the session carries
    ///      a fresh <c>step_up_verified_at</c> timestamp.
    /// </summary>
    /// <param name="factory">The shared <see cref="Infrastructure.HaliWebApplicationFactory"/>.</param>
    /// <param name="role">Either <c>"institution"</c> or <c>"institution_admin"</c>.
    ///     Determines the account's <c>IsInstitutionAdmin</c> flag, which
    ///     controls the role snapshotted onto the session row.</param>
    /// <param name="institutionId">Pre-existing institution id to tie the
    ///     account to. When null, the helper seeds a new one.</param>
    /// <param name="withStepUp">When true, the helper enrols TOTP and
    ///     confirms the current code so <c>step_up_verified_at</c> is
    ///     fresh. Required by privileged write endpoints.</param>
    public static async Task<InstitutionAuthSession> CreateSessionAsync(
        Infrastructure.HaliWebApplicationFactory factory,
        string role = "institution",
        Guid? institutionId = null,
        bool withStepUp = false,
        CancellationToken ct = default)
    {
        if (role is not ("institution" or "institution_admin"))
        {
            throw new ArgumentException(
                $"Role must be 'institution' or 'institution_admin'; got '{role}'. " +
                "Citizen callers should use CreateBearerClient.",
                nameof(role));
        }

        Guid actualInstitutionId = institutionId ?? await CreateInstitutionAsync(ct);
        Guid accountId = await CreateInstitutionAccountAsync(
            factory, actualInstitutionId,
            isAdmin: role == "institution_admin", ct);

        string email = await GetAccountEmailAsync(factory, accountId, ct);

        // Write the token row directly to avoid hitting the real Redis rate
        // limiter — test seeding must not exhaust the 3-per-15-min bucket.
        string plaintextToken;
        {
            byte[] raw = RandomNumberGenerator.GetBytes(32);
            plaintextToken = Convert.ToBase64String(raw);
            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
            string tokenHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            await using var conn = new NpgsqlConnection(Infrastructure.TestConstants.ConnectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO magic_link_tokens (id, destination_email, token_hash, account_id, expires_at, created_at)
                VALUES (@id, @email, @hash, @aid, @exp, @now)
                """, conn);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("hash", tokenHash);
            cmd.Parameters.AddWithValue("aid", accountId);
            cmd.Parameters.AddWithValue("exp", DateTime.UtcNow.AddMinutes(15));
            cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var client = factory.CreateClient();
        var verify = await client.PostAsJsonAsync(
            "/v1/auth/institution/magic-link/verify",
            new { token = plaintextToken }, s_json, ct);
        verify.EnsureSuccessStatusCode();

        string? csrf = ExtractCookieValue(verify, CsrfCookieName);
        string? session = ExtractCookieValue(verify, SessionCookieName);
        Assert.False(string.IsNullOrEmpty(csrf), "CSRF cookie must be set on magic-link verify");
        Assert.False(string.IsNullOrEmpty(session), "Session cookie must be set on magic-link verify");

        if (withStepUp)
        {
            await CompleteTotpStepUpAsync(factory, client, csrf!, accountId, ct);
        }

        return new InstitutionAuthSession(
            client, accountId, actualInstitutionId, csrf!, session!);
    }

    /// <summary>
    /// Mints a bearer JWT for tests that explicitly exercise the
    /// Bearer-authentication path. The token is validated by the real
    /// <c>TokenValidationParameters</c> configured in <c>Program.cs</c> —
    /// <see cref="Infrastructure.TestConstants"/> values are mirrored
    /// into the host's config by <c>HaliWebApplicationFactory</c>.
    ///
    /// Not a backdoor: bearer-authed institution JWTs cannot satisfy the
    /// step-up gate (that lives under the cookie + TOTP surface) and
    /// cannot set the institution-session middleware's per-request
    /// <c>WebSession</c> context. Tests that want a full cookie session
    /// should call <see cref="CreateSessionAsync"/> instead.
    /// </summary>
    public static HttpClient CreateBearerClient(
        Infrastructure.HaliWebApplicationFactory factory,
        Guid accountId,
        string role,
        Guid? institutionId = null)
    {
        string jwt = MintJwt(accountId, role, institutionId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    /// <summary>
    /// POSTs with the double-submit CSRF header required by every
    /// cookie-scoped write endpoint. Mirrors the existing per-test
    /// <c>PostWithCsrfAsync</c> helpers so call sites can switch without
    /// changing behaviour.
    /// </summary>
    public static async Task<HttpResponseMessage> PostWithCsrfAsync(
        InstitutionAuthSession session, string path, object? body = null,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Add("X-CSRF-Token", session.CsrfPlaintext);
        req.Content = body is null
            ? new StringContent(string.Empty, Encoding.UTF8, "application/json")
            : JsonContent.Create(body, options: s_json);
        return await session.Client.SendAsync(req, ct);
    }

    /// <summary>PUT with the double-submit CSRF header.</summary>
    public static async Task<HttpResponseMessage> PutWithCsrfAsync(
        InstitutionAuthSession session, string path, object body,
        CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, path);
        req.Headers.Add("X-CSRF-Token", session.CsrfPlaintext);
        req.Content = JsonContent.Create(body, options: s_json);
        return await session.Client.SendAsync(req, ct);
    }

    // ======================================================================
    // Internals
    // ======================================================================

    private static async Task<Guid> CreateInstitutionAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(Infrastructure.TestConstants.ConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), @name, 'utility', true, now())
RETURNING id", conn);
        cmd.Parameters.AddWithValue("name", $"Helper Test Inst {Guid.NewGuid():N}");
        return (Guid)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<Guid> CreateInstitutionAccountAsync(
        Infrastructure.HaliWebApplicationFactory factory,
        Guid institutionId,
        bool isAdmin,
        CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        // UUID-suffixed email guarantees uniqueness — the accounts table
        // enforces uq_accounts_email, so a shared static email would fail
        // on the second invocation within the same test (or across tests
        // when CleanTablesAsync has not run).
        string email = $"helper-{(isAdmin ? "admin" : "user")}-{Guid.NewGuid():N}@example.com";
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = email,
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            IsInstitutionAdmin = isAdmin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return account.Id;
    }

    private static async Task<string> GetAccountEmailAsync(
        Infrastructure.HaliWebApplicationFactory factory,
        Guid accountId,
        CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        Account account = await db.Accounts.FirstAsync(a => a.Id == accountId, ct);
        return account.Email!;
    }

    private static async Task CompleteTotpStepUpAsync(
        Infrastructure.HaliWebApplicationFactory factory,
        HttpClient client,
        string csrfPlaintext,
        Guid accountId,
        CancellationToken ct)
    {
        // Enroll TOTP. First-time enrollment returns the encoded secret
        // via the otpauth URI, but the encrypted copy is also persisted
        // to totp_secrets — we read it back from the DB rather than
        // parsing the URI.
        using (var enrollReq = new HttpRequestMessage(HttpMethod.Post,
            "/v1/auth/institution/totp/enroll"))
        {
            enrollReq.Headers.Add("X-CSRF-Token", csrfPlaintext);
            enrollReq.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var enroll = await client.SendAsync(enrollReq, ct);
            enroll.EnsureSuccessStatusCode();
        }

        string currentCode = await ComputeCurrentTotpCodeAsync(factory, accountId, ct);

        using var confirmReq = new HttpRequestMessage(HttpMethod.Post,
            "/v1/auth/institution/totp/confirm");
        confirmReq.Headers.Add("X-CSRF-Token", csrfPlaintext);
        confirmReq.Content = JsonContent.Create(new { code = currentCode }, options: s_json);
        var confirm = await client.SendAsync(confirmReq, ct);
        confirm.EnsureSuccessStatusCode();
    }

    private static async Task<string> ComputeCurrentTotpCodeAsync(
        Infrastructure.HaliWebApplicationFactory factory,
        Guid accountId,
        CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        TotpSecret secret = await db.TotpSecrets.FirstAsync(s => s.AccountId == accountId, ct);

        // Purpose string must match TotpService.DataProtectionPurpose —
        // duplicated deliberately because the const is private. If that
        // purpose ever changes, this helper and TotpService must move
        // together.
        var protector = scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("hali-institution-totp-secrets");
        string base32 = protector.Unprotect(secret.SecretEncrypted);
        byte[] raw = TotpService.DecodeBase32(base32);
        long step = TotpService.GetCurrentStep(DateTimeOffset.UtcNow);
        return TotpService.ComputeCode(raw, step);
    }

    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }
        string prefix = cookieName + "=";
        foreach (var raw in cookies)
        {
            if (!raw.StartsWith(prefix, StringComparison.Ordinal)) continue;
            int semicolon = raw.IndexOf(';');
            return semicolon < 0 ? raw[prefix.Length..] : raw[prefix.Length..semicolon];
        }
        return null;
    }

    private static string MintJwt(Guid accountId, string role, Guid? institutionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Infrastructure.TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new(ClaimTypes.Role, role),
        };
        if (institutionId.HasValue)
        {
            claims.Add(new Claim("institution_id", institutionId.Value.ToString()));
        }
        var token = new JwtSecurityToken(
            issuer: Infrastructure.TestConstants.JwtIssuer,
            audience: Infrastructure.TestConstants.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// The authenticated state produced by
/// <see cref="InstitutionAuthHelper.CreateSessionAsync"/>. Holds the
/// HttpClient plus the plaintext CSRF token tests need to echo into the
/// X-CSRF-Token header for every write verb.
///
/// Implements <see cref="IDisposable"/> so test call sites can write
/// <c>using var session = await InstitutionAuthHelper.CreateSessionAsync(...)</c>
/// and have the underlying HttpClient disposed with the test's scope —
/// the old JWT-minted pattern used <c>using var client = ...</c>, and
/// this keeps the same deterministic cleanup contract.
/// </summary>
public sealed class InstitutionAuthSession : IDisposable
{
    private bool _disposed;

    public HttpClient Client { get; }
    public Guid AccountId { get; }
    public Guid InstitutionId { get; }
    public string CsrfPlaintext { get; }
    public string SessionPlaintext { get; }

    internal InstitutionAuthSession(
        HttpClient client,
        Guid accountId,
        Guid institutionId,
        string csrfPlaintext,
        string sessionPlaintext)
    {
        Client = client;
        AccountId = accountId;
        InstitutionId = institutionId;
        CsrfPlaintext = csrfPlaintext;
        SessionPlaintext = sessionPlaintext;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Client.Dispose();
    }
}

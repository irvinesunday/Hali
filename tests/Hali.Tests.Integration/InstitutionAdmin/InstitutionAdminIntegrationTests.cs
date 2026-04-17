using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
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
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.InstitutionAdmin;

/// <summary>
/// End-to-end integration coverage for the Phase 2 institution-admin
/// routes (#196): list / detail / invite / role-change / scope.
/// Covers happy paths, forbidden-path (institution-member + citizen),
/// cross-institution isolation, step-up gating on writes, role
/// elevation guard, and the last-admin-demote guard.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionAdminIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InstitutionAdminIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -----------------------------------------------------------------------
    // GET /users
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListUsers_InstitutionAdmin_ReturnsOwnInstitutionUsers()
    {
        var (institutionId, adminId, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = CreateBearerClient(adminId, "institution_admin", institutionId);

        var resp = await client.GetAsync("/v1/institution-admin/users");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var items = body.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        var ids = items.EnumerateArray().Select(i => Guid.Parse(i.GetProperty("id").GetString()!)).ToHashSet();
        Assert.Contains(adminId, ids);
        Assert.Contains(memberId, ids);
    }

    [Fact]
    public async Task ListUsers_InstitutionMember_Returns403()
    {
        var (institutionId, _, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = CreateBearerClient(memberId, "institution", institutionId);

        var resp = await client.GetAsync("/v1/institution-admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Citizen_Returns403()
    {
        using var client = CreateBearerClient(Guid.NewGuid(), "citizen", institutionId: null);
        var resp = await client.GetAsync("/v1/institution-admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/v1/institution-admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // -----------------------------------------------------------------------
    // GET /users/{id}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetUser_InSameInstitution_ReturnsDetail()
    {
        var (institutionId, adminId, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = CreateBearerClient(adminId, "institution_admin", institutionId);

        var resp = await client.GetAsync($"/v1/institution-admin/users/{memberId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(memberId.ToString(), body.GetProperty("id").GetString());
        Assert.Equal("institution_user", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task GetUser_CrossInstitution_Returns404_NotForbidden()
    {
        var (institutionA, adminA, _) = await SeedInstitutionWithAdminAndMemberAsync();
        var (_, _, memberB) = await SeedInstitutionWithAdminAndMemberAsync(
            institutionName: "Other Institution", emailPrefix: "other");

        using var adminAClient = CreateBearerClient(adminA, "institution_admin", institutionA);
        var resp = await adminAClient.GetAsync($"/v1/institution-admin/users/{memberB}");

        // 404 (not 403) so admin A cannot probe user IDs from institution B.
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.user_not_found",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // POST /users/invite (step-up required)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InviteUser_BearerJwt_Returns403StepUpRequired()
    {
        // Bearer-JWT flows cannot satisfy step-up (it lives under the
        // cookie session + TOTP surface). Even an institution_admin
        // bearer token is rejected on the write endpoint.
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = CreateBearerClient(adminId, "institution_admin", institutionId);

        var resp = await client.PostAsJsonAsync("/v1/institution-admin/users/invite", new
        {
            email = "newbie@example.com",
            role = "institution_user",
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("auth.step_up_required",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task InviteUser_WithStepUpSession_HappyPath_ReturnsInvite()
    {
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = await LogInAdminWithStepUpAsync(adminId, institutionId);

        var resp = await PostWithCsrfAsync(client, "/v1/institution-admin/users/invite", new
        {
            email = "newcomer@example.com",
            role = "institution_user",
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.True(Guid.TryParse(body.GetProperty("inviteId").GetString(), out _));
        Assert.True(body.TryGetProperty("expiresAt", out _));
    }

    [Fact]
    public async Task InviteUser_ElevationAttempt_Returns403ElevationGuard()
    {
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = await LogInAdminWithStepUpAsync(adminId, institutionId);

        var resp = await PostWithCsrfAsync(client, "/v1/institution-admin/users/invite", new
        {
            email = "elevated@example.com",
            role = "institution_admin",
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.elevation_requires_approval",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task InviteUser_EmailAlreadyInUse_Returns409()
    {
        var (institutionId, adminId, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        string existingEmail = await GetEmailAsync(memberId);

        using var client = await LogInAdminWithStepUpAsync(adminId, institutionId);
        var resp = await PostWithCsrfAsync(client, "/v1/institution-admin/users/invite", new
        {
            email = existingEmail,
            role = "institution_user",
        });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.email_already_in_use",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // PUT /users/{id}/role
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChangeRole_ElevationAttempt_Returns403()
    {
        var (institutionId, adminId, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = await LogInAdminWithStepUpAsync(adminId, institutionId);

        var resp = await PutWithCsrfAsync(client, $"/v1/institution-admin/users/{memberId}/role", new
        {
            role = "institution_admin",
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.elevation_requires_approval",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangeRole_DemoteLastAdmin_Returns409()
    {
        // Seed an institution with exactly ONE admin — demoting that
        // admin (even self) must be blocked to prevent a lockout.
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = await LogInAdminWithStepUpAsync(adminId, institutionId);

        var resp = await PutWithCsrfAsync(client, $"/v1/institution-admin/users/{adminId}/role", new
        {
            role = "institution_user",
        });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.last_admin_cannot_demote",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangeRole_DemoteAdmin_WithTwoAdmins_Succeeds()
    {
        // With 2 admins present, demoting one is allowed.
        var (institutionId, adminA, _) = await SeedInstitutionWithAdminAndMemberAsync();
        var adminB = await SeedSecondAdminAsync(institutionId, "adminB@example.com");

        using var client = await LogInAdminWithStepUpAsync(adminA, institutionId);
        var resp = await PutWithCsrfAsync(client, $"/v1/institution-admin/users/{adminB}/role", new
        {
            role = "institution_user",
        });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Confirm the flag was actually flipped.
        string roleAfter = await GetAccountRoleAsync(adminB);
        Assert.Equal("institution_user", roleAfter);
    }

    [Fact]
    public async Task ChangeRole_CrossInstitutionTarget_Returns404()
    {
        var (institutionA, adminA, _) = await SeedInstitutionWithAdminAndMemberAsync();
        var (_, _, memberB) = await SeedInstitutionWithAdminAndMemberAsync(
            institutionName: "Other", emailPrefix: "b");

        using var client = await LogInAdminWithStepUpAsync(adminA, institutionA);
        var resp = await PutWithCsrfAsync(client, $"/v1/institution-admin/users/{memberB}/role", new
        {
            role = "institution_user",
        });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.user_not_found",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // GET /scope
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScope_ReturnsInstitutionAndJurisdictions()
    {
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        await SeedJurisdictionAsync(institutionId, FakeLocalityLookupRepository.TestLocalityId);

        using var client = CreateBearerClient(adminId, "institution_admin", institutionId);
        var resp = await client.GetAsync("/v1/institution-admin/scope");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(institutionId.ToString(), body.GetProperty("institutionId").GetString());
        Assert.True(body.GetProperty("jurisdictions").GetArrayLength() >= 1);
    }

    // =======================================================================
    // Test helpers
    // =======================================================================

    private string? _csrfPlaintext;

    private async Task<HttpClient> LogInAdminWithStepUpAsync(Guid adminId, Guid institutionId)
    {
        // Mint a magic-link + verify + TOTP enroll + TOTP confirm
        // directly via the services. The end state: an institution-web
        // session cookie with step_up_verified_at stamped.
        await EnsureAdminEmailNormalisedAsync(adminId);
        string email = await GetEmailAsync(adminId);

        using var scope = Factory.Services.CreateScope();
        var magic = scope.ServiceProvider.GetRequiredService<IMagicLinkService>();
        MagicLinkIssued issued = await magic.IssueAsync(email, default);

        // Bind the magic link row to the admin's account id via direct
        // UPDATE — the real flow does this at IssueAsync time via the
        // email-to-account join, but the test wants a deterministic tie.
        await using (var conn = new NpgsqlConnection(TestConstants.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE magic_link_tokens SET account_id = @aid WHERE token_hash = @hash", conn);
            cmd.Parameters.AddWithValue("aid", adminId);
            cmd.Parameters.AddWithValue("hash", magic.HashToken(issued.PlaintextToken));
            await cmd.ExecuteNonQueryAsync();
        }

        var client = Factory.CreateClient();
        var verify = await client.PostAsJsonAsync("/v1/auth/institution/magic-link/verify", new
        {
            token = issued.PlaintextToken,
        });
        verify.EnsureSuccessStatusCode();
        _csrfPlaintext = ExtractCookieValue(verify, "hali_institution_csrf");

        // Enroll TOTP and confirm with the current code so the session
        // carries a fresh step_up_verified_at.
        var enroll = await PostWithCsrfAsync(client, "/v1/auth/institution/totp/enroll");
        enroll.EnsureSuccessStatusCode();

        string currentCode = await ComputeCurrentCodeAsync(adminId);
        var confirm = await PostWithCsrfAsync(client, "/v1/auth/institution/totp/confirm", new
        {
            code = currentCode,
        });
        confirm.EnsureSuccessStatusCode();

        // The session row was created with role snapshotted from Account.
        // The seed flagged adminId as IsInstitutionAdmin=true, so role=
        // "institution_admin" on the session row.
        return client;
    }

    private async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        if (_csrfPlaintext is not null) req.Headers.Add("X-CSRF-Token", _csrfPlaintext);
        req.Content = body is null
            ? new StringContent("", Encoding.UTF8, "application/json")
            : JsonContent.Create(body);
        return await client.SendAsync(req);
    }

    private async Task<HttpResponseMessage> PutWithCsrfAsync(HttpClient client, string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, path);
        if (_csrfPlaintext is not null) req.Headers.Add("X-CSRF-Token", _csrfPlaintext);
        req.Content = JsonContent.Create(body);
        return await client.SendAsync(req);
    }

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

    private async Task<string> ComputeCurrentCodeAsync(Guid accountId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var secret = await db.TotpSecrets.FirstAsync(s => s.AccountId == accountId);
        var protector = scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("hali-institution-totp-secrets");
        string base32 = protector.Unprotect(secret.SecretEncrypted);
        byte[] raw = TotpService.DecodeBase32(base32);
        long step = TotpService.GetCurrentStep(DateTimeOffset.UtcNow);
        return TotpService.ComputeCode(raw, step);
    }

    // ---- Seeding ------------------------------------------------------

    private async Task<(Guid institutionId, Guid adminId, Guid memberId)>
        SeedInstitutionWithAdminAndMemberAsync(
            string institutionName = "Test Institution",
            string emailPrefix = "a")
    {
        Guid institutionId;
        await using (var conn = new NpgsqlConnection(TestConstants.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), @name, 'utility', true, now())
RETURNING id", conn);
            cmd.Parameters.AddWithValue("name", institutionName);
            institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var admin = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{emailPrefix}-admin-{Guid.NewGuid():N}@example.com",
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            IsInstitutionAdmin = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var member = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{emailPrefix}-member-{Guid.NewGuid():N}@example.com",
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            IsInstitutionAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Accounts.Add(admin);
        db.Accounts.Add(member);
        await db.SaveChangesAsync();

        return (institutionId, admin.Id, member.Id);
    }

    private async Task<Guid> SeedSecondAdminAsync(Guid institutionId, string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}-{email}".ToLowerInvariant(),
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            IsInstitutionAdmin = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static async Task SeedJurisdictionAsync(Guid institutionId, Guid localityId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institution_jurisdictions (id, institution_id, locality_id, created_at)
VALUES (gen_random_uuid(), @instId, @locId, now())", conn);
        cmd.Parameters.AddWithValue("instId", institutionId);
        cmd.Parameters.AddWithValue("locId", localityId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string> GetEmailAsync(Guid accountId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = await db.Accounts.FirstAsync(a => a.Id == accountId);
        return account.Email!;
    }

    private async Task<string> GetAccountRoleAsync(Guid accountId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = await db.Accounts.FirstAsync(a => a.Id == accountId);
        return account.IsInstitutionAdmin ? "institution_admin" : "institution_user";
    }

    private async Task EnsureAdminEmailNormalisedAsync(Guid accountId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = await db.Accounts.FirstAsync(a => a.Id == accountId);
        if (account.Email is not null)
        {
            account.Email = account.Email.ToLowerInvariant();
            await db.SaveChangesAsync();
        }
    }

    // ---- JWT minting for bearer-auth tests ---------------------------

    private HttpClient CreateBearerClient(Guid accountId, string role, Guid? institutionId)
    {
        var jwt = MintJwt(accountId, role, institutionId);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private static string MintJwt(Guid accountId, string role, Guid? institutionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new System.Collections.Generic.List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };
        if (institutionId.HasValue)
        {
            claims.Add(new Claim("institution_id", institutionId.Value.ToString()));
        }
        var token = new JwtSecurityToken(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Auth;
using Hali.Tests.Integration.Helpers;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.InstitutionAdmin;

/// <summary>
/// End-to-end integration coverage for the Phase 2 institution-admin
/// routes (#196): list / detail / invite / role-change / scope.
/// Covers happy paths, forbidden-path (institution-member + citizen),
/// cross-institution isolation, step-up gating on writes, role
/// elevation guard, and the last-admin-demote guard.
///
/// #241 — every auth setup goes through <see cref="InstitutionAuthHelper"/>.
/// Tests that exercise the bearer boundary (read endpoints under
/// [Authorize(Roles = "institution_admin")], citizen 403 checks, and
/// the "bearer-JWT cannot satisfy step-up" assertion) keep the bearer
/// path via <see cref="InstitutionAuthHelper.CreateBearerClient"/>
/// because that boundary IS the thing under test. Tests that require
/// a fresh step-up stamp use
/// <see cref="InstitutionAuthHelper.CreateSessionAsync"/> which drives
/// the real magic-link + TOTP-confirm flow.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionAdminIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InstitutionAdminIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -----------------------------------------------------------------------
    // GET /users — bearer-JWT auth (reads are not step-up gated)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListUsers_InstitutionAdmin_ReturnsOwnInstitutionUsers()
    {
        var (institutionId, adminId, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, adminId, role: "institution_admin", institutionId: institutionId);

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
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, memberId, role: "institution", institutionId: institutionId);

        var resp = await client.GetAsync("/v1/institution-admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Citizen_Returns403()
    {
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "citizen", institutionId: null);
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
    // GET /users/{id} — bearer-JWT auth
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetUser_InSameInstitution_ReturnsDetail()
    {
        var (institutionId, adminId, memberId) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, adminId, role: "institution_admin", institutionId: institutionId);

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

        using var adminAClient = InstitutionAuthHelper.CreateBearerClient(
            Factory, adminA, role: "institution_admin", institutionId: institutionA);
        var resp = await adminAClient.GetAsync($"/v1/institution-admin/users/{memberB}");

        // 404 (not 403) so admin A cannot probe user IDs from institution B.
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.user_not_found",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // POST /users/invite — bearer asserts step-up boundary; session drives happy path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InviteUser_BearerJwt_Returns403StepUpRequired()
    {
        // Bearer-JWT flows cannot satisfy step-up (it lives under the
        // cookie session + TOTP surface). Even an institution_admin
        // bearer token is rejected on the write endpoint. This assertion
        // IS the thing under test — keep the bearer path.
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, adminId, role: "institution_admin", institutionId: institutionId);

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
        // Helper creates the institution + admin; drives magic-link verify
        // and TOTP enroll + confirm so step_up_verified_at is fresh.
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", withStepUp: true);

        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/institution-admin/users/invite", new
            {
                email = $"newcomer-{Guid.NewGuid():N}@example.com",
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
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", withStepUp: true);

        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/institution-admin/users/invite", new
            {
                email = $"elevated-{Guid.NewGuid():N}@example.com",
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
        // Create institution + a member first so its email is reserved,
        // then log in an admin in the same institution via the helper
        // and attempt to invite using that member's email.
        var (institutionId, memberEmail) = await SeedInstitutionWithMemberAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", institutionId: institutionId,
            withStepUp: true);

        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/institution-admin/users/invite", new
            {
                email = memberEmail,
                role = "institution_user",
            });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.email_already_in_use",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // PUT /users/{id}/role — session + step-up required
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ChangeRole_ElevationAttempt_Returns403()
    {
        var (institutionId, memberId, _) = await SeedInstitutionWithMemberIdAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", institutionId: institutionId,
            withStepUp: true);

        var resp = await InstitutionAuthHelper.PutWithCsrfAsync(
            session, $"/v1/institution-admin/users/{memberId}/role", new
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
        // Helper creates the single admin in a fresh institution; trying
        // to demote that admin (self) must be blocked to prevent lockout.
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", withStepUp: true);

        var resp = await InstitutionAuthHelper.PutWithCsrfAsync(
            session, $"/v1/institution-admin/users/{session.AccountId}/role", new
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
        // Two admins in the institution: the helper's (session.AccountId)
        // + a seeded second admin (adminB). Demoting adminB is allowed.
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", withStepUp: true);
        Guid adminB = await SeedSecondAdminAsync(session.InstitutionId);

        var resp = await InstitutionAuthHelper.PutWithCsrfAsync(
            session, $"/v1/institution-admin/users/{adminB}/role", new
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
        // Helper logs the admin into institution A; member B lives in a
        // separately seeded institution B.
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", withStepUp: true);
        var (_, memberB) = await SeedInstitutionWithMemberAsync(
            institutionName: "Other", memberEmailPrefix: "b");

        var resp = await InstitutionAuthHelper.PutWithCsrfAsync(
            session, $"/v1/institution-admin/users/{memberB}/role", new
            {
                role = "institution_user",
            });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("institution_admin.user_not_found",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // GET /scope — bearer-JWT auth
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetScope_ReturnsInstitutionAndJurisdictions()
    {
        var (institutionId, adminId, _) = await SeedInstitutionWithAdminAndMemberAsync();
        await SeedJurisdictionAsync(institutionId, FakeLocalityLookupRepository.TestLocalityId);

        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, adminId, role: "institution_admin", institutionId: institutionId);
        var resp = await client.GetAsync("/v1/institution-admin/scope");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(institutionId.ToString(), body.GetProperty("institutionId").GetString());
        Assert.True(body.GetProperty("jurisdictions").GetArrayLength() >= 1);
    }

    // =======================================================================
    // Seed helpers
    // =======================================================================

    /// <summary>
    /// Seeds a fresh institution + an admin account + a member account.
    /// Returned ids are used by tests that authenticate via bearer JWT
    /// and therefore need control over the account identities on either
    /// side of the auth boundary.
    /// </summary>
    private async Task<(Guid institutionId, Guid adminId, Guid memberId)>
        SeedInstitutionWithAdminAndMemberAsync(
            string institutionName = "Test Institution",
            string emailPrefix = "a")
    {
        Guid institutionId = await InsertInstitutionAsync(institutionName);

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

    /// <summary>
    /// Seeds an institution + a single member account (no admin — the
    /// admin comes from <see cref="InstitutionAuthHelper.CreateSessionAsync"/>
    /// in tests that need a fresh step-up session). Returns both the
    /// institution id AND the member's email so callers testing the
    /// "email already in use" path don't need a second DB round-trip.
    /// </summary>
    private async Task<(Guid institutionId, string memberEmail)>
        SeedInstitutionWithMemberAsync(
            string institutionName = "Test Institution",
            string memberEmailPrefix = "member")
    {
        Guid institutionId = await InsertInstitutionAsync(institutionName);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var member = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{memberEmailPrefix}-{Guid.NewGuid():N}@example.com",
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            IsInstitutionAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Accounts.Add(member);
        await db.SaveChangesAsync();

        return (institutionId, member.Email!);
    }

    /// <summary>
    /// Variant of <see cref="SeedInstitutionWithMemberAsync"/> that also
    /// returns the member account id — callers that need the id to
    /// construct a role-change URL, not just the email.
    /// </summary>
    private async Task<(Guid institutionId, Guid memberId, string memberEmail)>
        SeedInstitutionWithMemberIdAsync(string institutionName = "Test Institution")
    {
        Guid institutionId = await InsertInstitutionAsync(institutionName);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var member = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"member-{Guid.NewGuid():N}@example.com",
            IsEmailVerified = true,
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            IsInstitutionAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Accounts.Add(member);
        await db.SaveChangesAsync();

        return (institutionId, member.Id, member.Email!);
    }

    /// <summary>
    /// Seeds an additional admin account in an existing institution so
    /// the "two admins present, demoting one succeeds" scenario has a
    /// demotion target that isn't the self-demote last-admin guard.
    /// </summary>
    private async Task<Guid> SeedSecondAdminAsync(Guid institutionId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"admin2-{Guid.NewGuid():N}@example.com",
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

    private static async Task<Guid> InsertInstitutionAsync(string name)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), @name, 'utility', true, now())
RETURNING id", conn);
        cmd.Parameters.AddWithValue("name", name);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
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

    private async Task<string> GetAccountRoleAsync(Guid accountId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = await db.Accounts.FirstAsync(a => a.Id == accountId);
        return account.IsInstitutionAdmin ? "institution_admin" : "institution_user";
    }
}

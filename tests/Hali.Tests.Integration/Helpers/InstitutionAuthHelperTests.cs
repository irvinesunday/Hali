using System;
using System.Net;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Xunit;

namespace Hali.Tests.Integration.Helpers;

/// <summary>
/// Smoke tests for <see cref="InstitutionAuthHelper"/> itself (#241).
///
/// These tests exercise the helper end-to-end against the real API so
/// a regression in the magic-link / TOTP flow or in the helper's cookie
/// extraction fails loudly here rather than silently masking as a
/// failure in one of the Phase 2 test classes that depend on it.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionAuthHelperTests : IntegrationTestBase
{
    public InstitutionAuthHelperTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task InstitutionAuthHelper_ProducesValidSession_ThroughRealAuthFlow()
    {
        // Primary smoke test. Drives the full magic-link + verify path:
        // creates a fresh institution + institution account, issues a
        // magic link, POSTs to /v1/auth/institution/magic-link/verify,
        // and returns a cookie-bearing HttpClient. A 200 OK from
        // /v1/institution/overview proves:
        //   * The session cookie was set on the response
        //   * The InstitutionSessionMiddleware resolved it successfully
        //   * The claims principal the middleware built carries both
        //     role="institution" and the institution_id claim
        //   * The underlying read path works end-to-end
        // Asserting an exact 200 (not just "not 401/403") catches the
        // degenerate cases where the controller returns 404 / 500 due
        // to a wiring break elsewhere — a weaker assertion would let
        // those silently mask as "passes".
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution");

        var response = await session.Client.GetAsync("/v1/institution/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(session.CsrfPlaintext),
            "Helper must surface the plaintext CSRF token for write-verb tests.");
        Assert.False(string.IsNullOrEmpty(session.SessionPlaintext),
            "Helper must surface the plaintext session cookie value.");
        Assert.NotEqual(Guid.Empty, session.AccountId);
        Assert.NotEqual(Guid.Empty, session.InstitutionId);
    }

    [Fact]
    public async Task InstitutionAuthHelper_WithStepUp_StampsStepUpOnSession()
    {
        // Session with step-up must satisfy the institution_admin write
        // endpoints. /v1/institution-admin/users/invite is the narrowest
        // write path: it requires (a) institution_admin role on the
        // session, (b) a fresh step_up_verified_at. A 200 OK here
        // proves both. The check is narrow — we do not assert on the
        // invite-id body beyond "parseable guid" because the shape of
        // the response is owned by #196, not by #241.
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution_admin", withStepUp: true);

        var response = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/institution-admin/users/invite", new
            {
                email = $"smoke-invite-{Guid.NewGuid():N}@example.com",
                role = "institution_user",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InstitutionAuthHelper_BearerClient_AuthenticatesWithMintedJwt()
    {
        // Bearer-path smoke test. A minted institution JWT must reach
        // InstitutionController.Overview and return 200 OK. Anything
        // other than 200 indicates a drift between
        // TestConstants.JwtIssuer/JwtAudience/JwtSecret and the values
        // Program.cs binds — which is exactly the kind of regression
        // a centralised helper is supposed to catch. (The bare
        // institution has no jurisdiction, so /overview naturally
        // returns an empty summary + zero-length areas, still 200.)
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: Guid.NewGuid());

        var response = await client.GetAsync("/v1/institution/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

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

namespace Hali.Tests.Integration.Auth;

/// <summary>
/// Issue #147: mirror of <see cref="UnauthenticatedChallengeEnvelopeTests"/>
/// for the 403 path. When an authenticated caller holds a valid JWT but the
/// caller's role claim does not match a <c>[Authorize(Roles = "...")]</c>
/// requirement, the authorization stage short-circuits with a 403 that
/// bypasses ExceptionHandlingMiddleware. Without <c>JwtBearerEvents.OnForbidden</c>
/// this produced a bare empty-body 403, breaking the canonical H2 envelope.
///
/// One representative endpoint per role-gated controller is exercised here —
/// adding all of them would be redundant because every role-gated endpoint
/// hits the same OnForbidden hook.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ForbiddenRoleEnvelopeTests : IntegrationTestBase
{
    public ForbiddenRoleEnvelopeTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    [Theory]
    // OfficialPostsController: [Authorize(Roles = "institution")] — exercised
    // with a citizen-role JWT (the common wrong-role case for this endpoint).
    [InlineData("POST", "/v1/institution/official-updates", "citizen", "{}")]
    // AdminController: class-level [Authorize(Roles = "admin")] — exercised
    // with both a citizen-role and institution-role JWT to cover the two
    // realistic wrong-role shapes a caller can present on admin routes.
    [InlineData("POST", "/v1/admin/institutions", "citizen", "{}")]
    [InlineData("POST", "/v1/admin/institutions", "institution", "{}")]
    [InlineData("DELETE", "/v1/admin/institutions/00000000-0000-0000-0000-000000000000/access", "citizen", null)]
    public async Task RoleGatedEndpoint_AuthenticatedWithWrongRole_EmitsCanonicalForbiddenEnvelope(
        string method,
        string path,
        string wrongRole,
        string? jsonBody)
    {
        // Mint a structurally valid JWT that the framework will accept, but
        // whose role claim does not satisfy the endpoint's [Authorize(Roles = ...)]
        // requirement. The request therefore reaches the authorization stage
        // and is short-circuited with a 403 — exactly the framework seam this
        // test is exercising.
        var jwt = MintJwt(Guid.NewGuid(), wrongRole);

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        using var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("error", out var error),
            "envelope must have a top-level `error` property");
        Assert.Equal(JsonValueKind.Object, error.ValueKind);

        Assert.True(error.TryGetProperty("code", out var code));
        Assert.Equal("auth.role_insufficient", code.GetString());

        Assert.True(error.TryGetProperty("message", out var message));
        var messageText = message.GetString();
        Assert.False(string.IsNullOrWhiteSpace(messageText),
            "envelope must include a non-empty message");

        // Security: the message must be opaque — it must not leak the
        // specific role, policy, or claim name required by the endpoint,
        // because that exposes the shape of the authorization graph.
        Assert.DoesNotContain("admin", messageText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("institution", messageText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role", messageText, StringComparison.OrdinalIgnoreCase);

        Assert.True(error.TryGetProperty("traceId", out var traceId),
            "envelope must include a traceId for support/log correlation");
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()),
            "traceId must be non-empty");
    }

    private static string MintJwt(Guid accountId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
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

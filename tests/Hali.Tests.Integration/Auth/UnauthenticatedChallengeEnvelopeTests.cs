using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Xunit;

namespace Hali.Tests.Integration.Auth;

/// <summary>
/// Issue #137: ASP.NET Core's JwtBearer challenge would short-circuit
/// [Authorize]-protected endpoints with a bare 401 (empty body) before
/// ExceptionHandlingMiddleware could write the canonical envelope. The
/// fix wires JwtBearerEvents.OnChallenge to emit
/// { error: { code: "auth.unauthenticated", message, traceId } } and
/// preserve the WWW-Authenticate: Bearer challenge header.
///
/// One representative endpoint per protected controller is exercised
/// here. Adding all of them would be redundant — every protected
/// endpoint hits the same OnChallenge code path.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class UnauthenticatedChallengeEnvelopeTests : IntegrationTestBase
{
    public UnauthenticatedChallengeEnvelopeTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    [Theory]
    [InlineData("GET", "/v1/users/me", null)]
    [InlineData("GET", "/v1/localities/followed", null)]
    [InlineData("PUT", "/v1/localities/followed", "{\"items\":[]}")]
    [InlineData("POST", "/v1/signals/submit", "{}")]
    [InlineData("POST", "/v1/devices/push-token", "{}")]
    [InlineData("POST", "/v1/institution/official-updates", "{}")]
    [InlineData("POST", "/v1/clusters/00000000-0000-0000-0000-000000000000/participation", "{}")]
    [InlineData("POST", "/v1/clusters/00000000-0000-0000-0000-000000000000/context", "{}")]
    [InlineData("POST", "/v1/clusters/00000000-0000-0000-0000-000000000000/restoration-response", "{}")]
    [InlineData("POST", "/v1/admin/institutions", "{}")]
    [InlineData("DELETE", "/v1/admin/institutions/00000000-0000-0000-0000-000000000000/access", null)]
    public async Task ProtectedEndpoint_NoAuthHeader_EmitsCanonicalUnauthenticatedEnvelope(
        string method,
        string path,
        string? jsonBody)
    {
        // Important: deliberately NO Authorization header on the request.
        // The framework JwtBearer challenge fires before the controller runs.
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        using var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        // RFC 7235: 401 responses MUST advertise the challenge scheme.
        Assert.True(
            response.Headers.WwwAuthenticate.Count > 0,
            "401 response must include WWW-Authenticate challenge header");
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("error", out var error),
            "envelope must have a top-level `error` property");
        Assert.Equal(JsonValueKind.Object, error.ValueKind);

        Assert.True(error.TryGetProperty("code", out var code));
        Assert.Equal("auth.unauthenticated", code.GetString());

        Assert.True(error.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()),
            "envelope must include a non-empty message");

        Assert.True(error.TryGetProperty("traceId", out var traceId),
            "envelope must include a traceId for support/log correlation");
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()),
            "traceId must be non-empty");
    }
}

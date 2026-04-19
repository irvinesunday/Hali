using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Middleware;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Domain.Entities.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Auth;

/// <summary>
/// Unit coverage for <see cref="InstitutionCsrfMiddleware"/>:
/// GET requests are skipped; POST without X-CSRF-Token returns 403;
/// POST with wrong token returns 403; POST with correct token passes through.
/// </summary>
public sealed class InstitutionCsrfMiddlewareTests
{
    // -----------------------------------------------------------------------
    // GET requests are not CSRF-checked
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task CsrfMiddleware_SafeVerb_SkipsValidation(string method)
    {
        bool nextCalled = false;
        var middleware = new InstitutionCsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        DefaultHttpContext ctx = BuildContext(method, hasSession: false, csrfHeader: null);
        var sessions = new FakeSessionService(csrfHash: "any");

        await middleware.InvokeAsync(ctx, sessions);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Requests without a session cookie are not CSRF-checked
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CsrfMiddleware_NoSession_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = new InstitutionCsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        DefaultHttpContext ctx = BuildContext("POST", hasSession: false, csrfHeader: null);
        var sessions = new FakeSessionService(csrfHash: "any");

        await middleware.InvokeAsync(ctx, sessions);

        Assert.True(nextCalled);
    }

    // -----------------------------------------------------------------------
    // POST with session but missing X-CSRF-Token → 403
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CsrfMiddleware_MissingHeader_Returns403WithCsrfMissingCode()
    {
        var middleware = new InstitutionCsrfMiddleware(_ => Task.CompletedTask);

        DefaultHttpContext ctx = BuildContext("POST", hasSession: true, csrfHeader: null);
        var sessions = new FakeSessionService(csrfHash: "expected-hash");

        await middleware.InvokeAsync(ctx, sessions);

        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
        var body = await ReadBodyAsync(ctx);
        Assert.Equal(ErrorCodes.AuthCsrfMissing, body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // POST with session and wrong X-CSRF-Token → 403
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CsrfMiddleware_MismatchedToken_Returns403WithCsrfMismatchCode()
    {
        var middleware = new InstitutionCsrfMiddleware(_ => Task.CompletedTask);

        // Session has csrfTokenHash = SHA-256 of "correct-csrf"
        string correctPlaintext = "correct-csrf";
        string correctHash = Sha256Hex(correctPlaintext);
        DefaultHttpContext ctx = BuildContext("POST", hasSession: true, csrfHeader: "wrong-value");
        StashSession(ctx, csrfTokenHash: correctHash);
        var sessions = new FakeSessionService(csrfHash: correctHash);

        await middleware.InvokeAsync(ctx, sessions);

        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
        var body = await ReadBodyAsync(ctx);
        Assert.Equal(ErrorCodes.AuthCsrfMismatch, body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // POST with correct X-CSRF-Token → passes through
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CsrfMiddleware_ValidToken_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = new InstitutionCsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        string plaintext = "correct-csrf-value";
        string hash = Sha256Hex(plaintext);
        DefaultHttpContext ctx = BuildContext("POST", hasSession: true, csrfHeader: plaintext);
        StashSession(ctx, csrfTokenHash: hash);
        // FakeSessionService.HashCsrfToken must return the same hash for the plaintext.
        var sessions = new FakeSessionService(csrfHash: hash);

        await middleware.InvokeAsync(ctx, sessions);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    private static DefaultHttpContext BuildContext(
        string method, bool hasSession, string? csrfHeader)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Response.Body = new MemoryStream();
        if (csrfHeader is not null)
            ctx.Request.Headers["X-CSRF-Token"] = csrfHeader;
        if (hasSession)
            StashSession(ctx, csrfTokenHash: "any-hash");
        return ctx;
    }

    private static void StashSession(DefaultHttpContext ctx, string csrfTokenHash)
    {
        ctx.Items["InstitutionWebSession"] = new WebSession
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SessionTokenHash = "session-hash",
            CsrfTokenHash = csrfTokenHash,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            AbsoluteExpiresAt = DateTime.UtcNow.AddHours(12),
        };
    }

    private static async Task<JsonElement> ReadBodyAsync(DefaultHttpContext ctx)
    {
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Response.Body);
    }

    private static string Sha256Hex(string input)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ======================================================================
    // Fake session service — only HashCsrfToken is used by the middleware
    // ======================================================================

    private sealed class FakeSessionService : IInstitutionSessionService
    {
        private readonly string _storedCsrfHash;

        public FakeSessionService(string csrfHash) => _storedCsrfHash = csrfHash;

        public string HashCsrfToken(string plaintext) => Sha256Hex(plaintext);

        public string HashSessionToken(string plaintext) => Sha256Hex(plaintext);

        public Task<SessionCreated> CreateAsync(Guid accountId, Guid? institutionId, string role, CancellationToken ct)
            => throw new NotImplementedException();
        public Task<SessionValidation> ValidateAsync(string sessionTokenPlaintext, CancellationToken ct)
            => throw new NotImplementedException();
        public Task TouchAsync(Guid sessionId, CancellationToken ct)
            => throw new NotImplementedException();
        public Task RevokeAsync(Guid sessionId, CancellationToken ct)
            => throw new NotImplementedException();
        public Task MarkStepUpVerifiedAsync(Guid sessionId, CancellationToken ct)
            => throw new NotImplementedException();
    }
}

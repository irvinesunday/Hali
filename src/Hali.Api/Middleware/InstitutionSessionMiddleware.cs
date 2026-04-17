using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Errors;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Domain.Entities.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Api.Middleware;

/// <summary>
/// Resolves and validates institution-web sessions from the
/// <c>hali_institution_session</c> cookie on requests under
/// <c>/v1/institution*</c>. When a session is present AND valid, the
/// middleware attaches a <see cref="ClaimsPrincipal"/> describing the
/// caller so downstream controllers consume the same pattern as the
/// JWT flow. When the cookie is invalid / expired / idle-expired, a
/// canonical <c>ApiErrorResponse</c> is written and the pipeline is
/// short-circuited.
///
/// The middleware deliberately ignores requests that carry a JWT Bearer
/// token — citizen flows continue to use JWT and must not be subjected
/// to the cookie's stricter timeout model.
/// </summary>
public sealed class InstitutionSessionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InstitutionSessionMiddleware> _logger;

    // NOTE on DI lifetime:
    //   Conventional middleware (via app.UseMiddleware<T>()) is instantiated
    //   once from the ROOT service provider. Injecting a scoped dependency
    //   here would capture it as a singleton — across requests it would
    //   either share a DbContext or operate on a disposed scope. Scoped
    //   services are resolved per-request via the InvokeAsync parameters
    //   below, which ASP.NET Core resolves from the request scope.
    public InstitutionSessionMiddleware(
        RequestDelegate next,
        ILogger<InstitutionSessionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IInstitutionSessionService sessions,
        IOptions<InstitutionAuthOptions> options)
    {
        InstitutionAuthOptions opts = options.Value;
        // Only act on institution/institution-admin paths; citizen routes
        // continue to rely on JwtBearer.
        var path = context.Request.Path.Value ?? string.Empty;
        bool isInstitutionPath =
            path.StartsWith("/v1/institution", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/v1/institution-admin", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/v1/auth/institution/session", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/v1/auth/institution/totp", StringComparison.OrdinalIgnoreCase);

        if (!isInstitutionPath)
        {
            await _next(context);
            return;
        }

        // Bearer tokens win if present — lets the existing JWT flow work
        // for integration tests that mint institution JWTs directly.
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader)
            && authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(opts.SessionCookieName, out var sessionToken)
            || string.IsNullOrWhiteSpace(sessionToken))
        {
            // No cookie — fall through. If the downstream endpoint is
            // [Authorize] it will fail via the usual 401 path. If it is
            // [AllowAnonymous] (e.g. magic-link request) it will work.
            await _next(context);
            return;
        }

        var validation = await sessions.ValidateAsync(sessionToken, context.RequestAborted);
        switch (validation.Result)
        {
            case SessionValidationResult.Ok:
                break;
            case SessionValidationResult.IdleTimeout:
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                    ErrorCodes.AuthInstitutionSessionIdleTimeout,
                    "Session timed out due to inactivity.");
                return;
            case SessionValidationResult.AbsoluteTimeout:
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                    ErrorCodes.AuthInstitutionSessionAbsoluteTimeout,
                    "Session expired — please sign in again.");
                return;
            default:
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized,
                    ErrorCodes.AuthInstitutionSessionInvalid,
                    "Session is invalid.");
                return;
        }

        WebSession session = validation.Session!;

        // Awaited (not fire-and-forget) — the scoped DbContext behind
        // ISessionService is disposed with the request scope, so a
        // background continuation would risk running on a disposed
        // context. A single UPDATE statement is cheap enough to carry
        // inline. Also update the in-memory session object so the
        // controller's RefreshSession response echoes the fresh
        // last-activity timestamp.
        DateTime touchedAt = DateTime.UtcNow;
        await sessions.TouchAsync(session.Id, context.RequestAborted);
        session.LastActivityAt = touchedAt;

        // Build a ClaimsPrincipal mirroring the JWT-flow shape. Role +
        // institution_id must match the existing [Authorize(Roles = ...)]
        // attributes on institution controllers. NameIdentifier maps to
        // the account id so the existing `User.FindFirstValue(NameIdentifier)`
        // pattern in controllers works without modification.
        var claims = new System.Collections.Generic.List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, session.AccountId.ToString()),
            new Claim(ClaimTypes.Role, "institution"),
        };
        if (session.InstitutionId.HasValue)
        {
            claims.Add(new Claim("institution_id", session.InstitutionId.Value.ToString()));
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "institution_session"));

        // Stash the session on HttpContext so controllers can read
        // step_up_verified_at and CSRF state without re-querying.
        context.Items["InstitutionWebSession"] = session;

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted) return;
        var traceId = context.Items["CorrelationId"] as string
                      ?? context.TraceIdentifier
                      ?? string.Empty;
        var envelope = new ApiErrorResponse
        {
            Error = new ApiErrorBody { Code = code, Message = message, TraceId = traceId },
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, ApiErrorJsonOptions.Default));
    }
}

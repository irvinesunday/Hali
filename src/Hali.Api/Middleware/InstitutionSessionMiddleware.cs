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
    private readonly IInstitutionSessionService _sessions;
    private readonly InstitutionAuthOptions _opts;
    private readonly ILogger<InstitutionSessionMiddleware> _logger;

    public InstitutionSessionMiddleware(
        RequestDelegate next,
        IInstitutionSessionService sessions,
        IOptions<InstitutionAuthOptions> options,
        ILogger<InstitutionSessionMiddleware> logger)
    {
        _next = next;
        _sessions = sessions;
        _opts = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
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

        if (!context.Request.Cookies.TryGetValue(_opts.SessionCookieName, out var sessionToken)
            || string.IsNullOrWhiteSpace(sessionToken))
        {
            // No cookie — fall through. If the downstream endpoint is
            // [Authorize] it will fail via the usual 401 path. If it is
            // [AllowAnonymous] (e.g. magic-link request) it will work.
            await _next(context);
            return;
        }

        var validation = await _sessions.ValidateAsync(sessionToken, context.RequestAborted);
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

        // Touch the session asynchronously so the idle timer resets
        // without blocking the request pipeline. The write is
        // fire-and-forget: the read already snapshotted the current
        // last_activity_at and a lost update only costs one request
        // worth of idle time.
        _ = _sessions.TouchAsync(session.Id, CancellationToken.None);

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

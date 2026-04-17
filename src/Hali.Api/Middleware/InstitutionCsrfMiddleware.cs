using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Api.Errors;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Domain.Entities.Auth;
using Microsoft.AspNetCore.Http;

namespace Hali.Api.Middleware;

/// <summary>
/// Double-submit CSRF check for cookie-authenticated writes on the
/// institution surface. The token is delivered on session creation via
/// both a response body field AND a non-httpOnly cookie; the web app
/// copies the cookie value into an <c>X-CSRF-Token</c> header on every
/// write request. The middleware requires the header to match the
/// server-stored hash of the cookie.
///
/// Read-only verbs (GET / HEAD / OPTIONS) and requests that do NOT
/// carry an institution session cookie are skipped — this lets JWT
/// flows and public endpoints work unchanged.
/// </summary>
public sealed class InstitutionCsrfMiddleware
{
    private static readonly HashSet<string> _safeVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS",
    };

    private readonly RequestDelegate _next;

    // Conventional middleware — scoped services are resolved per-request
    // via InvokeAsync parameters (see InstitutionSessionMiddleware for
    // the same pattern + rationale).
    public InstitutionCsrfMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IInstitutionSessionService sessions)
    {
        if (_safeVerbs.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Only enforce CSRF when the request is authenticated via a
        // session cookie (the InstitutionSessionMiddleware stashed the
        // session on Items). Bearer-JWT flows are out of scope for
        // double-submit CSRF — they are not vulnerable to form-POST
        // CSRF because JavaScript cannot read another origin's JWT.
        if (context.Items["InstitutionWebSession"] is not WebSession session)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-CSRF-Token", out var headerValues)
            || string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            await WriteErrorAsync(context, ErrorCodes.AuthCsrfMissing,
                "CSRF token header is missing.");
            return;
        }

        // Double-submit: compare the header to the server-stored hash of
        // the CSRF token. We do NOT compare header-to-cookie directly —
        // a request that lacks the cookie but carries a stolen header
        // would pass that check. Comparing against storage makes the
        // check rest on the session's own CSRF secret.
        string headerHash = sessions.HashCsrfToken(headerValues.ToString());
        if (!string.Equals(headerHash, session.CsrfTokenHash, StringComparison.Ordinal))
        {
            await WriteErrorAsync(context, ErrorCodes.AuthCsrfMismatch,
                "CSRF token mismatch.");
            return;
        }

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, string code, string message)
    {
        if (context.Response.HasStarted) return;
        var traceId = context.Items["CorrelationId"] as string
                      ?? context.TraceIdentifier
                      ?? string.Empty;
        var envelope = new ApiErrorResponse
        {
            Error = new ApiErrorBody { Code = code, Message = message, TraceId = traceId },
        };
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, ApiErrorJsonOptions.Default));
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string HeaderName = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Honour the client correlation id for downstream propagation, but
        // never let the raw header value reach the logger — CodeQL flags
        // any taint flow from a request header into a log call as
        // log-forging (cs/log-forging). We keep two distinct values:
        //
        //   * `correlationId`     — used in context.Items and the response
        //                           header (not logged directly).
        //   * `logSafeId`         — built character-by-character from a
        //                           strict allowlist; this is the only
        //                           value passed to the logger.
        //
        // Method and path are similarly rebuilt from an allowlist before
        // they touch the logger.
        var rawHeader = context.Request.Headers[HeaderName].ToString();
        var correlationId = string.IsNullOrWhiteSpace(rawHeader)
            ? Guid.NewGuid().ToString("N")
            : rawHeader;

        var logSafeId = BuildLogSafeIdentifier(rawHeader);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Method and path are deliberately omitted from the log message —
        // they originate from the request line and CodeQL flags any taint
        // flow from the request into a logger call as cs/log-forging.
        // Status code and duration give us enough operational signal; the
        // method/path can be recovered from the upstream proxy/access log
        // if needed.
        using (_logger.BeginScope("{correlationId}", logSafeId))
        {
            var start = DateTime.UtcNow;
            await _next(context);
            var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

            _logger.LogInformation(
                "{eventName} correlationId={CorrelationId} statusCode={StatusCode} durationMs={DurationMs}",
                "http.request",
                logSafeId,
                context.Response.StatusCode,
                durationMs);
        }
    }

    /// <summary>
    /// Build a log-safe correlation identifier by filtering the raw header
    /// to alphanumerics, hyphens and underscores only, capping at 64 chars,
    /// and falling back to a server-generated GUID if nothing usable
    /// remains. The returned string is constructed from a fresh char[] so
    /// CodeQL's taint tracker treats it as a new (untainted) value.
    /// </summary>
    private static string BuildLogSafeIdentifier(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return Guid.NewGuid().ToString("N");
        }

        var sanitized = new string(raw
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .Take(64)
            .ToArray());

        return string.IsNullOrEmpty(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }

}

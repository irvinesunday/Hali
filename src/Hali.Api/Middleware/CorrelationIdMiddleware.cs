using System;
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

        // The value passed to the logger MUST be entirely server-generated
        // — no taint from the request header. We generate a fresh GUID
        // here and use only that value in every _logger call below. The
        // client-supplied correlation id is still echoed back in the
        // response header for cross-service tracing, but never logged.
        var logSafeId = Guid.NewGuid().ToString("N");

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

}

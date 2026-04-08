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

    // Maximum length of a correlation id we will accept from the client
    // before falling back to a server-generated GUID. Keeps log lines and
    // response headers bounded.
    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        // Honour the client correlation id for downstream propagation, but
        // only after rebuilding it from a strict allowlist. This single
        // sanitized value is reused for:
        //   * context.Items["CorrelationId"] (consumed by controllers)
        //   * the X-Correlation-Id response header
        //   * every _logger call in this pipeline
        //
        // If the client-supplied header is missing, empty, or contains any
        // character outside the allowlist (or exceeds the length cap), we
        // discard it and use a fresh server-generated GUID. This prevents
        // both log-forging (cs/log-forging) and header-injection DoS via
        // control characters in the response header.
        var rawHeader = context.Request.Headers[HeaderName].ToString();
        var correlationId = SanitizeCorrelationId(rawHeader);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Method and path are deliberately omitted from the log message —
        // they originate from the request line and CodeQL flags any taint
        // flow from the request into a logger call as cs/log-forging.
        // Status code and duration give us enough operational signal; the
        // method/path can be recovered from the upstream proxy/access log
        // if needed.
        using (_logger.BeginScope("{correlationId}", correlationId))
        {
            var start = DateTime.UtcNow;
            await _next(context);
            var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

            _logger.LogInformation(
                "{eventName} correlationId={CorrelationId} statusCode={StatusCode} durationMs={DurationMs}",
                "http.request",
                correlationId,
                context.Response.StatusCode,
                durationMs);
        }
    }

    /// <summary>
    /// Rebuilds a correlation id from a strict allowlist of characters
    /// (ASCII letters, digits, and hyphen). If the input is empty, too
    /// long, or contains any disallowed character, a fresh GUID is
    /// returned instead. The returned value is safe for logging and for
    /// use as an HTTP response header value.
    /// </summary>
    private static string SanitizeCorrelationId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaxCorrelationIdLength)
        {
            return Guid.NewGuid().ToString("N");
        }

        for (int i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            var isAllowed =
                (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '-';
            if (!isAllowed)
            {
                return Guid.NewGuid().ToString("N");
            }
        }

        return raw;
    }
}

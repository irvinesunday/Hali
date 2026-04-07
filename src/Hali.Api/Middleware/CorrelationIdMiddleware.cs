using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string HeaderName = "X-Correlation-Id";

    // Allow only printable ASCII identifiers — alphanumerics, hyphens and
    // underscores — up to 64 characters. This covers standard UUID format
    // (32 hex chars or 36 with hyphens) and short trace IDs while rejecting
    // anything that could carry CR/LF or other control characters used in
    // log-injection attacks.
    private static readonly Regex CorrelationIdPattern =
        new Regex("^[A-Za-z0-9\\-_]{1,64}$", RegexOptions.Compiled);

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headerValue = context.Request.Headers[HeaderName].ToString();
        var correlationId = SanitizeCorrelationId(headerValue);

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Sanitize HTTP method and path before they touch the logger so that
        // a malicious request line cannot inject forged log entries.
        var safeMethod = SanitizeForLog(context.Request.Method, 16);
        var safePath = SanitizeForLog(context.Request.Path.Value, 256);

        using (_logger.BeginScope("{correlationId} {method} {path}",
            correlationId,
            safeMethod,
            safePath))
        {
            var start = DateTime.UtcNow;
            await _next(context);
            var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

            _logger.LogInformation(
                "{eventName} correlationId={CorrelationId} method={Method} path={Path} statusCode={StatusCode} durationMs={DurationMs}",
                "http.request",
                correlationId,
                safeMethod,
                safePath,
                context.Response.StatusCode,
                durationMs);
        }
    }

    private static string SanitizeCorrelationId(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || !CorrelationIdPattern.IsMatch(raw))
        {
            return Guid.NewGuid().ToString("N");
        }
        return raw;
    }

    /// <summary>
    /// Strip CR/LF and other control characters from user-controlled values
    /// before they are written to logs, and bound the length to prevent log
    /// flooding. This blocks the log-injection attack class flagged by
    /// CodeQL rule cs/log-forging.
    /// </summary>
    private static string SanitizeForLog(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var span = value.AsSpan();
        var buffer = new char[Math.Min(span.Length, maxLength)];
        var written = 0;
        for (int i = 0; i < span.Length && written < buffer.Length; i++)
        {
            var c = span[i];
            if (c == '\r' || c == '\n' || char.IsControl(c))
            {
                buffer[written++] = '_';
            }
            else
            {
                buffer[written++] = c;
            }
        }
        return new string(buffer, 0, written);
    }
}

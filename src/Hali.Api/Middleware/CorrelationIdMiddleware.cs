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
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(correlationId))
            correlationId = Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope("{correlationId} {method} {path}",
            correlationId,
            context.Request.Method,
            context.Request.Path))
        {
            var start = DateTime.UtcNow;
            await _next(context);
            var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

            _logger.LogInformation(
                "{eventName} correlationId={CorrelationId} method={Method} path={Path} statusCode={StatusCode} durationMs={DurationMs}",
                "http.request",
                correlationId,
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                durationMs);
        }
    }
}

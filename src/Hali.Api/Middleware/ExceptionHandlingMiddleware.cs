using System;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Api.Errors;
using Hali.Application.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly ExceptionToApiErrorMapper _mapper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        ExceptionToApiErrorMapper mapper)
    {
        _next = next;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an error. Let the framework handle
            // the abort without emitting a 500 envelope or error-level log.
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(exception,
                "Response already started; cannot write error envelope for {ExceptionType}",
                exception.GetType().Name);
            return;
        }

        var mapping = _mapper.Map(exception);
        var correlationId = SanitizeCorrelationId(
            context.Items["CorrelationId"] as string ?? "");

        LogException(exception, mapping, context);

        var response = new ApiErrorResponse
        {
            Error = new ApiErrorBody
            {
                Code = mapping.Code,
                Message = mapping.Message,
                Details = mapping.Details,
                TraceId = correlationId
            }
        };

        context.Response.StatusCode = mapping.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    /// <summary>
    /// Strip characters that could enable log injection. The correlation ID
    /// originates from the X-Correlation-Id request header (user-controlled)
    /// so it must be sanitized before inclusion in the response body.
    /// </summary>
    private static string SanitizeCorrelationId(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var span = raw.AsSpan();
        Span<char> buf = stackalloc char[Math.Min(span.Length, 64)];
        int pos = 0;
        foreach (char c in span)
        {
            if (pos >= buf.Length) break;
            if (char.IsLetterOrDigit(c) || c == '-')
                buf[pos++] = c;
        }
        return new string(buf[..pos]);
    }

    // The correlation ID is already attached to the log scope by
    // CorrelationIdMiddleware.BeginScope, so it is intentionally omitted
    // from the log template to avoid duplication and CodeQL cs/log-forging
    // taint findings (the value originates from the X-Correlation-Id header).
    private const string LogTemplate =
        "{EventName} error.code={ErrorCode} error.category={ErrorCategory} " +
        "http.status_code={StatusCode} route={Route} method={Method}";

    private void LogException(Exception exception, ApiErrorMapping mapping, HttpContext context)
    {
        var category = (exception as AppException)?.Category.ToString() ?? "unexpected";
        var route = context.GetEndpoint()?.DisplayName ?? "unknown";

        // Error+ includes the exception object so stack traces appear in logs;
        // lower severities log structured fields only (no stack trace noise).
        if (mapping.LogLevel >= LogLevel.Error)
        {
            _logger.Log(mapping.LogLevel, exception, LogTemplate,
                "api.exception_handled", mapping.Code, category,
                mapping.StatusCode, route, context.Request.Method);
        }
        else
        {
            _logger.Log(mapping.LogLevel, LogTemplate,
                "api.exception_handled", mapping.Code, category,
                mapping.StatusCode, route, context.Request.Method);
        }
    }
}

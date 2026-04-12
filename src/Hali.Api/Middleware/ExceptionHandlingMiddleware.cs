using System;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Api.Errors;
using Hali.Application.Errors;
using Microsoft.AspNetCore.Http;
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
            context.Items["CorrelationId"] as string
            ?? context.TraceIdentifier);

        LogException(exception, mapping);

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

    // All request-derived values (path, method, correlation ID) are excluded
    // from this template to satisfy CodeQL cs/log-forging. The correlation
    // ID and request metadata are already captured in the log scope by
    // CorrelationIdMiddleware.BeginScope — they appear in every log entry
    // without needing to be repeated here.
    private void LogException(Exception exception, ApiErrorMapping mapping)
    {
        var category = (exception as AppException)?.Category.ToString() ?? "unexpected";

        // Error+ includes the exception object so stack traces appear in logs;
        // lower severities log structured fields only (no stack trace noise).
        if (mapping.LogLevel >= LogLevel.Error)
        {
            _logger.Log(mapping.LogLevel, exception,
                "{EventName} error.code={ErrorCode} error.category={ErrorCategory} http.status_code={StatusCode}",
                "api.exception_handled", mapping.Code, category, mapping.StatusCode);
        }
        else
        {
            _logger.Log(mapping.LogLevel,
                "{EventName} error.code={ErrorCode} error.category={ErrorCategory} http.status_code={StatusCode}",
                "api.exception_handled", mapping.Code, category, mapping.StatusCode);
        }
    }
}

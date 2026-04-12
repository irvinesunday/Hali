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
        var correlationId = context.Items["CorrelationId"] as string ?? "";

        LogException(exception, mapping, correlationId, context);

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

    private const string LogTemplate =
        "{EventName} error.code={ErrorCode} error.category={ErrorCategory} " +
        "http.status_code={StatusCode} trace.id={TraceId} " +
        "route={Route} method={Method}";

    private void LogException(Exception exception, ApiErrorMapping mapping, string correlationId, HttpContext context)
    {
        var category = (exception as AppException)?.Category.ToString() ?? "unexpected";
        var args = new object?[]
        {
            "api.exception_handled", mapping.Code, category,
            mapping.StatusCode, correlationId,
            context.Request.Path.Value, context.Request.Method
        };

        // Error+ includes the exception object so stack traces appear in logs;
        // lower severities log structured fields only (no stack trace noise).
        if (mapping.LogLevel >= LogLevel.Error)
            _logger.Log(mapping.LogLevel, exception, LogTemplate, args);
        else
            _logger.Log(mapping.LogLevel, LogTemplate, args);
    }
}

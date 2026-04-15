using System;
using Hali.Application.Errors;
using Hali.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Errors;

public sealed class ExceptionToApiErrorMapper
{
    public ApiErrorMapping Map(Exception exception)
    {
        return exception switch
        {
            ValidationException ve => new ApiErrorMapping
            {
                StatusCode = 400,
                Code = ve.Code,
                Message = ve.Message,
                Details = ve.FieldErrors is { Count: > 0 }
                    ? new { fields = ve.FieldErrors }
                    : null,
                LogLevel = LogLevel.Information,
                Category = ErrorCategory.Validation
            },

            NotFoundException nfe => new ApiErrorMapping
            {
                StatusCode = 404,
                Code = nfe.Code,
                Message = nfe.Message,
                LogLevel = LogLevel.Information,
                Category = ErrorCategory.NotFound
            },

            ConflictException ce => new ApiErrorMapping
            {
                StatusCode = 409,
                Code = ce.Code,
                Message = ce.Message,
                LogLevel = LogLevel.Information,
                Category = ErrorCategory.Conflict
            },

            RateLimitException rle => new ApiErrorMapping
            {
                StatusCode = 429,
                Code = rle.Code,
                Message = rle.Message,
                LogLevel = LogLevel.Warning,
                Category = ErrorCategory.RateLimit
            },

            DependencyException de => new ApiErrorMapping
            {
                StatusCode = 503,
                Code = de.Code,
                Message = de.Message,
                LogLevel = LogLevel.Warning,
                Category = ErrorCategory.Dependency
            },

            UnauthorizedException ue => new ApiErrorMapping
            {
                StatusCode = 401,
                Code = ue.Code,
                Message = ue.Message,
                LogLevel = LogLevel.Warning,
                Category = ErrorCategory.Unauthorized
            },

            AppException ae => MapByCategory(ae),

            _ => new ApiErrorMapping
            {
                StatusCode = 500,
                Code = ErrorCodes.ServerInternalError,
                Message = "An unexpected error occurred.",
                LogLevel = LogLevel.Error,
                Category = ErrorCategory.Unexpected
            }
        };
    }

    private static ApiErrorMapping MapByCategory(AppException ae)
    {
        // ErrorCategory.Unexpected represents an internal invariant violation
        // carried by a typed AppException (e.g. InvariantViolationException).
        // The typed form preserves ae.Code for logs/traces, but the wire MUST
        // NOT leak the internal code — it is redacted to ServerInternalError,
        // matching the behaviour of the unmapped-exception fallback above.
        if (ae.Category == ErrorCategory.Unexpected)
        {
            return new ApiErrorMapping
            {
                StatusCode = 500,
                Code = ErrorCodes.ServerInternalError,
                Message = "An unexpected error occurred.",
                LogLevel = LogLevel.Error,
                // Category reflects the redacted wire outcome, not the
                // (identical) raw exception category — keeping this in the
                // mapping makes ApiMetrics agnostic to redaction semantics.
                Category = ErrorCategory.Unexpected
            };
        }

        var (statusCode, logLevel) = ae.Category switch
        {
            ErrorCategory.Validation => (400, LogLevel.Information),
            ErrorCategory.NotFound => (404, LogLevel.Information),
            ErrorCategory.Conflict => (409, LogLevel.Information),
            ErrorCategory.Unauthorized => (401, LogLevel.Warning),
            ErrorCategory.Forbidden => (403, LogLevel.Warning),
            ErrorCategory.RateLimit => (429, LogLevel.Warning),
            ErrorCategory.Integrity => (403, LogLevel.Warning),
            ErrorCategory.Dependency => (503, LogLevel.Warning),
            ErrorCategory.Timeout => (504, LogLevel.Warning),
            ErrorCategory.Infrastructure => (500, LogLevel.Error),
            _ => (500, LogLevel.Error)
        };

        return new ApiErrorMapping
        {
            StatusCode = statusCode,
            Code = ae.Code,
            Message = ae.Message,
            LogLevel = logLevel,
            Category = ae.Category
        };
    }
}

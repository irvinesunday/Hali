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
                LogLevel = LogLevel.Information
            },

            NotFoundException nfe => new ApiErrorMapping
            {
                StatusCode = 404,
                Code = nfe.Code,
                Message = nfe.Message,
                LogLevel = LogLevel.Information
            },

            ConflictException ce => new ApiErrorMapping
            {
                StatusCode = 409,
                Code = ce.Code,
                Message = ce.Message,
                LogLevel = LogLevel.Information
            },

            RateLimitException rle => new ApiErrorMapping
            {
                StatusCode = 429,
                Code = rle.Code,
                Message = rle.Message,
                LogLevel = LogLevel.Warning
            },

            DependencyException de => new ApiErrorMapping
            {
                StatusCode = 503,
                Code = de.Code,
                Message = de.Message,
                LogLevel = LogLevel.Warning
            },

            UnauthorizedException ue => new ApiErrorMapping
            {
                StatusCode = 401,
                Code = ue.Code,
                Message = ue.Message,
                LogLevel = LogLevel.Warning
            },

            AppException ae => MapByCategory(ae),

            _ => new ApiErrorMapping
            {
                StatusCode = 500,
                Code = "server.internal_error",
                Message = "An unexpected error occurred.",
                LogLevel = LogLevel.Error
            }
        };
    }

    private static ApiErrorMapping MapByCategory(AppException ae)
    {
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
            LogLevel = logLevel
        };
    }
}

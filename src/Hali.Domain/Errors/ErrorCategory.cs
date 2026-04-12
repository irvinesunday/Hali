namespace Hali.Domain.Errors;

public enum ErrorCategory
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    RateLimit,
    Integrity,
    Dependency,
    Timeout,
    Infrastructure,
    Unexpected
}

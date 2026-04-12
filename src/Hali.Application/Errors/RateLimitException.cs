using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class RateLimitException : AppException
{
    public RateLimitException(
        string code = "integrity.rate_limited",
        string message = "Too many requests. Please try again later.",
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorCategory.RateLimit, isTransient: true, metadata: metadata)
    {
    }
}

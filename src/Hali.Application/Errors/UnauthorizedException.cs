using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class UnauthorizedException : AppException
{
    public UnauthorizedException(
        string code = "auth.unauthorized",
        string message = "Authentication is required.",
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorCategory.Unauthorized, metadata: metadata)
    {
    }
}

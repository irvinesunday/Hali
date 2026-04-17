using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class ForbiddenException : AppException
{
    public ForbiddenException(
        string code = ErrorCodes.AuthForbidden,
        string message = "This action is not permitted.",
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorCategory.Forbidden, metadata: metadata)
    {
    }
}

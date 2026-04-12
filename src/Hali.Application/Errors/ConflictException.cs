using System;
using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class ConflictException : AppException
{
    public ConflictException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorCategory.Conflict, metadata: metadata)
    {
    }
}

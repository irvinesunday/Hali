using System;
using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class NotFoundException : AppException
{
    public NotFoundException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorCategory.NotFound, metadata: metadata)
    {
    }
}

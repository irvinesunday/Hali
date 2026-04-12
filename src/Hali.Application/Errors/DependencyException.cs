using System;
using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class DependencyException : AppException
{
    public DependencyException(
        string code,
        string message,
        bool isTransient = true,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? innerException = null)
        : base(code, message, ErrorCategory.Dependency, isTransient: isTransient, metadata: metadata, innerException: innerException)
    {
    }
}

using System;
using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public abstract class AppException : Exception
{
    public string Code { get; }
    public ErrorCategory Category { get; }
    public bool IsTransient { get; }
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    protected AppException(
        string code,
        string message,
        ErrorCategory category,
        bool isTransient = false,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Category = category;
        IsTransient = isTransient;
        Metadata = metadata;
    }
}

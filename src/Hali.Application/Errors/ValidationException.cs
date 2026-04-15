using System;
using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; }

    public ValidationException(
        string message,
        string code = ErrorCodes.ValidationFailed,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
        : base(code, message, ErrorCategory.Validation, metadata: metadata)
    {
        FieldErrors = fieldErrors;
    }
}

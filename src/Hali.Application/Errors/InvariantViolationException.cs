using System;
using System.Collections.Generic;
using Hali.Domain.Errors;

namespace Hali.Application.Errors;

/// <summary>
/// Thrown when an internal invariant is broken — a state the code assumes
/// cannot occur under correct use of the module (e.g. a signal reaching the
/// clustering pipeline without a derived spatial cell).
///
/// Carries <see cref="ErrorCategory.Unexpected"/>. The exception mapper
/// preserves the original <see cref="AppException.Code"/> in logs/traces for
/// debuggability but redacts the wire response to
/// <c>server.internal_error</c> so internal invariant names never reach
/// clients.
/// </summary>
public sealed class InvariantViolationException : AppException
{
    public InvariantViolationException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? innerException = null)
        : base(code, message, ErrorCategory.Unexpected, isTransient: false, metadata: metadata, innerException: innerException)
    {
    }
}

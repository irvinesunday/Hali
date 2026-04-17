using Hali.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Errors;

public sealed class ApiErrorMapping
{
    public int StatusCode { get; init; }
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public object? Details { get; init; }
    public LogLevel LogLevel { get; init; }

    /// <summary>
    /// Category of the mapped (wire-visible) error. For redacted internal
    /// invariant violations this is <see cref="ErrorCategory.Unexpected"/>,
    /// matching the redacted wire code. Used by <c>ApiMetrics</c> to tag
    /// the <c>api_exceptions_total</c> counter without re-deriving category
    /// from the raw exception.
    /// </summary>
    public ErrorCategory Category { get; init; }
}

using Microsoft.Extensions.Logging;

namespace Hali.Api.Errors;

public sealed class ApiErrorMapping
{
    public int StatusCode { get; init; }
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
    public object? Details { get; init; }
    public LogLevel LogLevel { get; init; }
}

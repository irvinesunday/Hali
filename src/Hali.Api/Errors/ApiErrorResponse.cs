using System.Text.Json.Serialization;

namespace Hali.Api.Errors;

public sealed class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public ApiErrorBody Error { get; init; } = default!;
}

public sealed class ApiErrorBody
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = default!;

    [JsonPropertyName("message")]
    public string Message { get; init; } = default!;

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }

    [JsonPropertyName("traceId")]
    public string TraceId { get; init; } = default!;
}

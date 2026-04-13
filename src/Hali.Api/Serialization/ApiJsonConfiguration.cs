using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Serialization;

/// <summary>
/// Single source of truth for API JSON serialization options.
///
/// Applies the two project-wide wire conventions documented in
/// <c>02_openapi.yaml</c>:
///   - Property names: camelCase (<see cref="JsonNamingPolicy.CamelCase"/>)
///   - Enum values: snake_case_lower via <see cref="JsonStringEnumConverter"/>
///     with <see cref="JsonNamingPolicy.SnakeCaseLower"/>
///
/// Referenced by <c>Program.cs</c> (production) and by contract-drift
/// tests, so the same config is never duplicated in two places.
/// </summary>
public static class ApiJsonConfiguration
{
    public static void Configure(JsonOptions options)
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    }
}

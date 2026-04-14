using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hali.Api.Errors;

/// <summary>
/// Single source of truth for the <see cref="JsonSerializerOptions"/> used
/// to serialise <see cref="ApiErrorResponse"/> envelopes from paths that
/// bypass the MVC pipeline — specifically <c>ExceptionHandlingMiddleware</c>
/// and the <c>JwtBearerEvents.OnChallenge</c> handler.
///
/// Keeps the error-envelope wire shape identical across every entry point:
/// camelCase property names, and <c>details</c> omitted from the payload
/// when null rather than serialised as <c>"details": null</c>.
///
/// Distinct from <see cref="Hali.Api.Serialization.ApiJsonConfiguration"/>,
/// which configures the MVC pipeline's <c>JsonOptions</c> (a different type
/// from the standalone <c>JsonSerializerOptions</c> used here).
/// </summary>
public static class ApiErrorJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

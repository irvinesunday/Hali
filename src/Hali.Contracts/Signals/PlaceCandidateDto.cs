using System;

namespace Hali.Contracts.Signals;

/// <summary>
/// A single geocoding candidate returned by the low-confidence location
/// fallback endpoints (<c>GET /v1/places/search</c> and
/// <c>GET /v1/places/reverse</c>).
///
/// The composer treats this DTO as authoritative: when the user picks a
/// candidate, <see cref="Latitude"/>/<see cref="Longitude"/> become the
/// submitted coordinates and <see cref="DisplayName"/> becomes the
/// submitted <c>locationLabel</c>. Callers MUST NOT synthesize candidates
/// client-side without backing coordinates — the backend's spatial
/// integrity rules still apply on submit.
/// </summary>
public class PlaceCandidateDto
{
    public string DisplayName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>
    /// The Hali locality (ward) that contains <see cref="Latitude"/>/<see cref="Longitude"/>,
    /// if the point falls inside a known boundary. Candidates outside known
    /// localities are filtered out at the API boundary so the mobile client
    /// never surfaces a place the user cannot actually submit.
    /// </summary>
    public Guid? LocalityId { get; set; }

    public string? WardName { get; set; }

    public string? CityName { get; set; }
}

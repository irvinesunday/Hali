using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Signals;

public record GeocodingCandidate(string DisplayName, double Latitude, double Longitude);

public interface IGeocodingService
{
    Task<GeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);

    /// <summary>
    /// Forward geocode a free-text query to up to 5 candidate places,
    /// biased to Kenya. Returns an empty list on failure or no matches.
    /// </summary>
    Task<IReadOnlyList<GeocodingCandidate>> SearchAsync(string query, CancellationToken ct = default);
}

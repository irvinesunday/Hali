using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Signals;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Geocoding service that always returns null — prevents real HTTP calls to
/// Nominatim during integration tests.
/// </summary>
internal sealed class FakeGeocodingService : IGeocodingService
{
    public Task<GeocodingResult?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
        => Task.FromResult<GeocodingResult?>(null);
}

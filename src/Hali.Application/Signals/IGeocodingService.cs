namespace Hali.Application.Signals;

public interface IGeocodingService
{
    /// <summary>
    /// Reverse geocodes lat/lng to location fields.
    /// Results are cached in Redis using key geocode:{lat}:{lng}.
    /// Returns null if geocoding fails or coordinates are null.
    /// </summary>
    Task<GeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
}

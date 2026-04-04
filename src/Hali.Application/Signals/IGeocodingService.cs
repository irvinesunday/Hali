using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Signals;

public interface IGeocodingService
{
	Task<GeocodingResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default(CancellationToken));
}

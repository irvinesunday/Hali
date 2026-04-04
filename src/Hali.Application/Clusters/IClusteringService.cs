using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Signals;

namespace Hali.Application.Clusters;

public interface IClusteringService
{
	Task RouteSignalAsync(SignalEvent signal, CancellationToken ct = default(CancellationToken));
}

using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Clusters;

public interface IOutboxRelayService
{
    /// <summary>
    /// Reads up to 100 unpublished outbox events, marks them published, and returns the count processed.
    /// </summary>
    Task<int> ProcessPendingAsync(CancellationToken ct = default);
}

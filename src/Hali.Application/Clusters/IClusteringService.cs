using Hali.Domain.Entities.Signals;

namespace Hali.Application.Clusters;

public interface IClusteringService
{
    /// <summary>
    /// Routes a persisted signal to an existing cluster (by join_score) or creates a new one.
    /// Emits outbox events for every cluster create or update.
    /// </summary>
    Task RouteSignalAsync(SignalEvent signal, CancellationToken ct = default);
}

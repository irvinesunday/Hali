using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Hali.Application.Clusters;

public class OutboxRelayService : IOutboxRelayService
{
    private const int BatchSize = 100;
    private const int StaleThresholdSeconds = 60;

    private readonly IClusterRepository _repo;
    private readonly ILogger<OutboxRelayService>? _logger;

    public OutboxRelayService(IClusterRepository repo, ILogger<OutboxRelayService>? logger = null)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken ct = default)
    {
        var events = await _repo.GetUnpublishedOutboxEventsAsync(BatchSize, ct);
        if (events.Count == 0)
            return 0;

        var stale = events.Count(e => (DateTime.UtcNow - e.OccurredAt).TotalSeconds > StaleThresholdSeconds);
        if (stale > 0)
            _logger?.LogWarning("Outbox relay: {StaleCount} event(s) older than {Threshold}s are pending — downstream consumers may be delayed",
                stale, StaleThresholdSeconds);

        var ids = events.Select(e => e.Id);
        await _repo.MarkOutboxEventsPublishedAsync(ids, ct);

        _logger?.LogDebug("Outbox relay processed {Count} events", events.Count);
        return events.Count;
    }
}

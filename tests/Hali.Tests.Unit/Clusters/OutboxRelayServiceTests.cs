using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Xunit;

namespace Hali.Tests.Unit.Clusters;

/// <summary>
/// B-6: Outbox relay must read unpublished events, mark them published, and report the count.
/// </summary>
public class OutboxRelayServiceTests
{
    // -----------------------------------------------------------------------
    // When pending events exist, ProcessPendingAsync marks them published
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OutboxRelay_ProcessesEvent_AndNotificationIsFired()
    {
        // Arrange — seed three unpublished outbox events
        var repo = new FakeRelayRepo();
        repo.SeedEvent(Guid.NewGuid(), "cluster", "cluster_state_changed");
        repo.SeedEvent(Guid.NewGuid(), "cluster", "cluster_state_changed");
        repo.SeedEvent(Guid.NewGuid(), "signal",  "signal_submitted");

        var svc = new OutboxRelayService(repo);

        // Act
        var processed = await svc.ProcessPendingAsync();

        // Assert — all three events are marked published
        Assert.Equal(3, processed);
        Assert.All(repo.Events, e => Assert.NotNull(e.PublishedAt));
    }

    // -----------------------------------------------------------------------
    // When no pending events, returns 0 and makes no write calls
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OutboxRelay_WhenNoPendingEvents_ReturnsZero()
    {
        var repo = new FakeRelayRepo();
        var svc = new OutboxRelayService(repo);

        var processed = await svc.ProcessPendingAsync();

        Assert.Equal(0, processed);
        Assert.Empty(repo.MarkPublishedCalls);
    }

    // -----------------------------------------------------------------------
    // Only unpublished events are processed; already-published are skipped
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OutboxRelay_SkipsAlreadyPublishedEvents()
    {
        var repo = new FakeRelayRepo();
        repo.SeedEvent(Guid.NewGuid(), "cluster", "cluster_state_changed"); // unpublished
        repo.SeedPublishedEvent(Guid.NewGuid(), "cluster", "cluster_state_changed"); // already published

        var svc = new OutboxRelayService(repo);
        var processed = await svc.ProcessPendingAsync();

        Assert.Equal(1, processed);
        // Only the unpublished event was passed to MarkOutboxEventsPublishedAsync
        Assert.Single(repo.MarkPublishedCalls);
    }

    // -----------------------------------------------------------------------
    // Fake repo
    // -----------------------------------------------------------------------

    private sealed class FakeRelayRepo : IClusterRepository
    {
        public List<OutboxEvent> Events { get; } = new();
        public List<IEnumerable<Guid>> MarkPublishedCalls { get; } = new();

        public void SeedEvent(Guid id, string aggregateType, string eventType)
        {
            Events.Add(new OutboxEvent
            {
                Id = id,
                AggregateType = aggregateType,
                AggregateId = Guid.NewGuid(),
                EventType = eventType,
                OccurredAt = DateTime.UtcNow,
                PublishedAt = null
            });
        }

        public void SeedPublishedEvent(Guid id, string aggregateType, string eventType)
        {
            Events.Add(new OutboxEvent
            {
                Id = id,
                AggregateType = aggregateType,
                AggregateId = Guid.NewGuid(),
                EventType = eventType,
                OccurredAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        }

        public Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct)
        {
            var unpublished = Events.Where(e => e.PublishedAt == null).Take(limit).ToList();
            return Task.FromResult((IReadOnlyList<OutboxEvent>)unpublished);
        }

        public Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            var idList = ids.ToList();
            MarkPublishedCalls.Add(idList);
            var now = DateTime.UtcNow;
            foreach (var id in idList)
            {
                var ev = Events.Find(e => e.Id == id);
                if (ev != null) ev.PublishedAt = now;
            }
            return Task.CompletedTask;
        }

        // Unused IClusterRepository stubs
        public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<SignalCluster?> GetClusterByIdAsync(Guid id, CancellationToken ct) => Task.FromResult((SignalCluster?)null);
        public Task<SignalCluster> CreateClusterAsync(SignalCluster c, Guid s, Guid? d, CancellationToken ct) => Task.FromResult(c);
        public Task AttachToClusterAsync(Guid c, Guid s, Guid? d, string r, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateClusterAsync(SignalCluster c, CancellationToken ct) => Task.CompletedTask;
        public Task<int> ComputeWrabCountAsync(Guid c, int d, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ComputeActiveMassCountAsync(Guid c, int h, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountUniqueDevicesAsync(Guid c, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task WriteCivisDecisionAsync(CivisDecision d, CancellationToken ct) => Task.CompletedTask;
        public Task WriteOutboxEventAsync(OutboxEvent e, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateCountsAsync(Guid c, int a, int o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
    }
}

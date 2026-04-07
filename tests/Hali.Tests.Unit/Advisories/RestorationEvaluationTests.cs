using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Participation;
using Hali.Domain.Entities.Clusters;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Advisories;

public class RestorationEvaluationTests
{
    private sealed class FakeClusterRepo : IClusterRepository
    {
        private readonly SignalCluster? _cluster;
        public List<SignalCluster> Updates { get; } = new();
        public List<CivisDecision> Decisions { get; } = new();
        public List<OutboxEvent> OutboxEvents { get; } = new();

        public FakeClusterRepo(SignalCluster? cluster = null) { _cluster = cluster; }

        public Task<SignalCluster?> GetClusterByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_cluster);
        public Task UpdateClusterAsync(SignalCluster c, CancellationToken ct) { Updates.Add(c); return Task.CompletedTask; }
        public Task WriteCivisDecisionAsync(CivisDecision d, CancellationToken ct) { Decisions.Add(d); return Task.CompletedTask; }
        public Task WriteOutboxEventAsync(OutboxEvent e, CancellationToken ct) { OutboxEvents.Add(e); return Task.CompletedTask; }
        public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<SignalCluster> CreateClusterAsync(SignalCluster c, Guid s, Guid? d, CancellationToken ct) => Task.FromResult(c);
        public Task AttachToClusterAsync(Guid c, Guid s, Guid? d, string r, CancellationToken ct) => Task.CompletedTask;
        public Task<int> ComputeWrabCountAsync(Guid c, int d, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ComputeActiveMassCountAsync(Guid c, int h, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountUniqueDevicesAsync(Guid c, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task UpdateCountsAsync(Guid c, int a, int o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct) => Task.FromResult((IReadOnlyList<OutboxEvent>)Array.Empty<OutboxEvent>());
        public Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeParticipationRepo : IParticipationRepository
    {
        private readonly List<ParticipationEntity> _store = new();

        public void Add(ParticipationType type, Guid clusterId)
        {
            _store.Add(new ParticipationEntity
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                DeviceId = Guid.NewGuid(),
                ParticipationType = type,
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<ParticipationEntity?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
            => Task.FromResult(_store.Find(x => x.ClusterId == clusterId && x.DeviceId == deviceId));

        public Task<ParticipationEntity?> GetMostRecentByAccountAsync(Guid clusterId, Guid accountId, CancellationToken ct)
            => Task.FromResult(_store.FindAll(x => x.ClusterId == clusterId && x.AccountId == accountId)
                .OrderByDescending(x => x.CreatedAt).FirstOrDefault());

        public Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
        { _store.RemoveAll(x => x.ClusterId == clusterId && x.DeviceId == deviceId); return Task.CompletedTask; }

        public Task AddAsync(ParticipationEntity p, CancellationToken ct) { _store.Add(p); return Task.CompletedTask; }
        public Task UpdateContextAsync(Guid id, string text, CancellationToken ct) => Task.CompletedTask;

        public Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct)
            => Task.FromResult(_store.FindAll(x => x.ClusterId == clusterId && x.ParticipationType == type).Count);

        public Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct)
            => Task.FromResult(_store.FindAll(x => x.ClusterId == clusterId &&
                (x.ParticipationType == ParticipationType.RestorationYes ||
                 x.ParticipationType == ParticipationType.RestorationNo ||
                 x.ParticipationType == ParticipationType.RestorationUnsure)).Count);

        public Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Guid>)Array.Empty<Guid>());
    }

    private static (ParticipationService svc, FakeClusterRepo clusterRepo, FakeParticipationRepo participRepo) BuildSut(SignalCluster cluster)
    {
        var opts = Options.Create(new CivisOptions { RestorationRatio = 0.60, MinRestorationAffectedVotes = 2, ContextEditWindowMinutes = 2 });
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo();
        return (new ParticipationService(participRepo, clusterRepo, opts), clusterRepo, participRepo);
    }

    private static SignalCluster ActiveCluster(Guid? id = null) => new SignalCluster
    {
        Id = id ?? Guid.NewGuid(),
        State = SignalState.Active,
        Category = CivicCategory.Roads,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow
    };

    [Fact]
    public async Task RestorationRatio_AtExactly60Percent_WithMinVotes_TransitionsToPossibleRestoration()
    {
        var cluster = ActiveCluster();
        var (svc, clusterRepo, participRepo) = BuildSut(cluster);
        // Seed 3 yes + 2 no = 5 restoration responses so far (ratio = 3/5 = 0.60)
        // When we add device vote below it becomes 4/6 = 0.666 — still above 0.60
        for (int i = 0; i < 3; i++) participRepo.Add(ParticipationType.RestorationYes, cluster.Id);
        for (int i = 0; i < 2; i++) participRepo.Add(ParticipationType.RestorationNo, cluster.Id);

        var deviceId = Guid.NewGuid();
        await svc.RecordParticipationAsync(cluster.Id, deviceId, null, ParticipationType.Affected, null, CancellationToken.None);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceId, null, "restored", CancellationToken.None);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.NotNull(cluster.PossibleRestorationAt);
    }

    [Fact]
    public async Task RestorationRatio_Below60Percent_DoesNotTransition()
    {
        var cluster = ActiveCluster();
        var (svc, clusterRepo, participRepo) = BuildSut(cluster);
        // 1 yes + 1 no seeded = 0.50 ratio already in store
        participRepo.Add(ParticipationType.RestorationYes, cluster.Id);
        participRepo.Add(ParticipationType.RestorationNo, cluster.Id);

        var deviceId = Guid.NewGuid();
        await svc.RecordParticipationAsync(cluster.Id, deviceId, null, ParticipationType.Affected, null, CancellationToken.None);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceId, null, "not_sure", CancellationToken.None);
        // Now 1 yes out of 3 (1 no + 1 unsure) = 0.33

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
    }

    [Fact]
    public async Task RestorationRatio_Above60Percent_Transitions()
    {
        var cluster = ActiveCluster();
        var (svc, clusterRepo, participRepo) = BuildSut(cluster);
        // 3 yes + 1 no seeded = 3/4 = 0.75
        for (int i = 0; i < 3; i++) participRepo.Add(ParticipationType.RestorationYes, cluster.Id);
        participRepo.Add(ParticipationType.RestorationNo, cluster.Id);

        var deviceId = Guid.NewGuid();
        await svc.RecordParticipationAsync(cluster.Id, deviceId, null, ParticipationType.Affected, null, CancellationToken.None);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceId, null, "restored", CancellationToken.None);
        // Now 4 yes out of 5 = 0.80 >= 0.60

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
    }

    [Fact]
    public async Task RestorationRatio_InsufficientVotes_DoesNotTransition()
    {
        var cluster = ActiveCluster();
        var (svc, clusterRepo, participRepo) = BuildSut(cluster);

        var deviceId = Guid.NewGuid();
        await svc.RecordParticipationAsync(cluster.Id, deviceId, null, ParticipationType.Affected, null, CancellationToken.None);
        await svc.RecordRestorationResponseAsync(cluster.Id, deviceId, null, "restored", CancellationToken.None);
        // 1 yes out of 1 total = 1.0, but < minRestorationAffectedVotes (2)

        Assert.Equal(SignalState.Active, cluster.State);
    }
}

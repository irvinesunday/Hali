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

namespace Hali.Tests.Unit.Clusters;

/// <summary>
/// Enforces the lifecycle centralization invariant from issue #210:
/// every legitimate cluster state transition must route through
/// <see cref="IClusterRepository.ApplyClusterTransitionAsync"/>.
///
/// Each test proves that for a specific transition:
/// (1) <c>ApplyClusterTransitionAsync</c> was called exactly once
/// (2) a <see cref="CivisDecision"/> was recorded atomically
/// (3) an <see cref="OutboxEvent"/> was emitted atomically
/// (4) standalone <c>UpdateClusterAsync</c> was NOT called for the transition
/// </summary>
public class LifecycleCentralizationTests
{
    // ── fake repo shared by all tests ─────────────────────────────────────────

    private sealed class TrackingClusterRepo : IClusterRepository
    {
        private readonly SignalCluster? _cluster;

        public int ApplyTransitionCallCount { get; private set; }
        public int StandaloneUpdateCallCount { get; private set; }
        public List<CivisDecision> Decisions { get; } = new();
        public List<OutboxEvent> OutboxEvents { get; } = new();

        // Configurable return values for CIVIS computation calls
        public int WrabCount { get; set; }
        public int ActiveMass { get; set; }
        public int UniqueDevices { get; set; }

        public TrackingClusterRepo(SignalCluster? cluster = null) { _cluster = cluster; }

        public Task<SignalCluster?> GetClusterByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_cluster);

        public Task ApplyClusterTransitionAsync(SignalCluster cluster, CivisDecision? decision, OutboxEvent outboxEvent, CancellationToken ct)
        {
            ApplyTransitionCallCount++;
            if (decision is not null) Decisions.Add(decision);
            OutboxEvents.Add(outboxEvent);
            return Task.CompletedTask;
        }

        public Task UpdateClusterAsync(SignalCluster c, CancellationToken ct)
        {
            StandaloneUpdateCallCount++;
            return Task.CompletedTask;
        }

        public Task<int> ComputeWrabCountAsync(Guid c, int d, CancellationToken ct) => Task.FromResult(WrabCount);
        public Task<int> ComputeActiveMassCountAsync(Guid c, int h, CancellationToken ct) => Task.FromResult(ActiveMass);
        public Task<int> CountUniqueDevicesAsync(Guid c, CancellationToken ct) => Task.FromResult(UniqueDevices);
        public Task<double> GetMinLocationConfidenceAsync(Guid c, CancellationToken ct) => Task.FromResult(1.0);
        public Task WriteCivisDecisionAsync(CivisDecision d, CancellationToken ct) { Decisions.Add(d); return Task.CompletedTask; }
        public Task WriteOutboxEventAsync(OutboxEvent e, CancellationToken ct) { OutboxEvents.Add(e); return Task.CompletedTask; }
        public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<SignalCluster> CreateClusterAsync(SignalCluster c, Guid s, Guid? d, CancellationToken ct) => Task.FromResult(c);
        public Task AttachToClusterAsync(Guid c, Guid s, Guid? d, string r, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task UpdateCountsAsync(Guid c, int a, int o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(IEnumerable<Guid> ids, bool? r, int limit, DateTime? cursor, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(IEnumerable<Guid> ids, int limit, DateTime? cursor, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct) => Task.FromResult((IReadOnlyList<OutboxEvent>)Array.Empty<OutboxEvent>());
        public Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>
    /// Full-fidelity participation fake that stores records in memory. Used by
    /// tests that need the participation guard in
    /// <see cref="ParticipationService.RecordRestorationResponseAsync"/> to pass.
    /// Exposes a configurable <see cref="RestorationSnapshot"/> for the snapshot
    /// query so the test controls which restoration-vote counts drive the transition.
    /// </summary>
    private sealed class FakeParticipationRepo : IParticipationRepository
    {
        private readonly List<ParticipationEntity> _store = new();
        public RestorationCountSnapshot RestorationSnapshot { get; set; } = new RestorationCountSnapshot(0, 0, 0);

        public Task<ParticipationEntity?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
            => Task.FromResult(_store.Find(x => x.ClusterId == clusterId && x.DeviceId == deviceId));
        public Task<ParticipationEntity?> GetMostRecentByAccountAsync(Guid c, Guid a, CancellationToken ct)
            => Task.FromResult(_store.FindAll(x => x.ClusterId == c && x.AccountId == a)
                .OrderByDescending(x => x.CreatedAt).FirstOrDefault());
        public Task DeleteByDeviceAsync(Guid c, Guid d, CancellationToken ct)
        { _store.RemoveAll(x => x.ClusterId == c && x.DeviceId == d); return Task.CompletedTask; }
        public Task AddAsync(ParticipationEntity p, CancellationToken ct) { _store.Add(p); return Task.CompletedTask; }
        public Task UpdateContextAsync(Guid id, string text, CancellationToken ct) => Task.CompletedTask;
        public Task<int> CountByTypeAsync(Guid c, ParticipationType t, CancellationToken ct)
            => Task.FromResult(_store.FindAll(x => x.ClusterId == c && x.ParticipationType == t).Count);
        public Task<RestorationCountSnapshot> GetRestorationCountSnapshotAsync(Guid c, CancellationToken ct)
            => Task.FromResult(RestorationSnapshot);
        public Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<Guid>)Array.Empty<Guid>());
        public async Task<IReadOnlyDictionary<Guid, RestorationCountSnapshot>> GetRestorationCountSnapshotsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            var result = new Dictionary<Guid, RestorationCountSnapshot>(ids.Count);
            foreach (var id in ids)
            {
                var snap = await GetRestorationCountSnapshotAsync(id, ct);
                if (snap.TotalResponses > 0) result[id] = snap;
            }
            return result;
        }
    }

    private static readonly IOptions<CivisOptions> DefaultOpts = Options.Create(new CivisOptions
    {
        WrabRollingWindowDays = 30,
        JoinThreshold = 0.65,
        MinUniqueDevices = 2,
        DeactivationThreshold = 0.5,
        ActiveMassHorizonHours = 48,
        TimeScoreMaxAgeHours = 24.0,
        RestorationRatio = 0.60,
        MinRestorationAffectedVotes = 2,
        ContextEditWindowMinutes = 2,
        Roads = new CivisCategoryOptions { BaseFloor = 2, HalfLifeHours = 18.0, MacfMin = 2, MacfMax = 6 }
    });

    // ── Path 1: CIVIS Activation (Unconfirmed → Active) ───────────────────────

    [Fact]
    public async Task Activation_RoutesThrough_ApplyClusterTransitionAsync()
    {
        var clusterId = Guid.NewGuid();
        var cluster = new SignalCluster
        {
            Id = clusterId,
            State = SignalState.Unconfirmed,
            Category = CivicCategory.Roads,
            // RawConfirmationCount must satisfy: count >= macf (computed below)
            // macf = ceil(baseFloor=2 + log2(1+sds)) = ceil(2 + log2(1+5)) = ceil(2+2.58) = 5
            // So 10 comfortably exceeds macf of 5.
            RawConfirmationCount = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        var repo = new TrackingClusterRepo(cluster)
        {
            // activeMass=10, wrab=1, baseFloor=2 → effectiveWrab=2, sds=10/2=5
            ActiveMass = 10,
            WrabCount = 1,
            // Must satisfy uniqueDevices >= MinUniqueDevices (2)
            UniqueDevices = 3
        };
        var sut = new CivisEvaluationService(repo, DefaultOpts);

        await sut.EvaluateClusterAsync(clusterId);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Equal(1, repo.ApplyTransitionCallCount);
        Assert.Single(repo.Decisions);
        Assert.Equal("activated", repo.Decisions[0].DecisionType);
        Assert.Single(repo.OutboxEvents);
        // Invariant: standalone UpdateClusterAsync was NOT called for this transition
        Assert.Equal(0, repo.StandaloneUpdateCallCount);
    }

    // ── Path 2: CIVIS Decay (Active → PossibleRestoration) ───────────────────

    [Fact]
    public async Task DecayActiveToRestoration_RoutesThrough_ApplyClusterTransitionAsync()
    {
        var clusterId = Guid.NewGuid();
        var cluster = new SignalCluster
        {
            Id = clusterId,
            State = SignalState.Active,
            Category = CivicCategory.Roads,
            RawConfirmationCount = 1,
            Wrab = 10,
            // 100 hours ago: with halfLife=18h, liveMass ≈ 0.021, effectiveWrab=10
            // liveMass/effectiveWrab ≈ 0.002 < deactivationThreshold=0.5 → decay fires
            LastSeenAt = DateTime.UtcNow.AddHours(-100),
            CreatedAt = DateTime.UtcNow.AddHours(-100),
            UpdatedAt = DateTime.UtcNow.AddHours(-100),
            FirstSeenAt = DateTime.UtcNow.AddHours(-100)
        };
        var repo = new TrackingClusterRepo(cluster);
        var sut = new CivisEvaluationService(repo, DefaultOpts);

        await sut.ApplyDecayAsync(clusterId);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.Equal(1, repo.ApplyTransitionCallCount);
        Assert.Single(repo.Decisions);
        Assert.Equal("possible_restoration", repo.Decisions[0].DecisionType);
        Assert.Single(repo.OutboxEvents);
        Assert.Equal(0, repo.StandaloneUpdateCallCount);
    }

    // ── Path 3: Citizen Restoration Vote (Active → PossibleRestoration) ───────

    [Fact]
    public async Task CitizenRestorationVote_RoutesThrough_ApplyClusterTransitionAsync()
    {
        var clusterId = Guid.NewGuid();
        var cluster = new SignalCluster
        {
            Id = clusterId,
            State = SignalState.Active,
            Category = CivicCategory.Roads,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        var clusterRepo = new TrackingClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo
        {
            // 3 yes out of 4 total = 0.75 ≥ 0.60, total = 4 ≥ min 2
            RestorationSnapshot = new RestorationCountSnapshot(YesVotes: 3, NoVotes: 1, TotalResponses: 4)
        };
        var sut = new ParticipationService(participRepo, clusterRepo, DefaultOpts);

        var deviceId = Guid.NewGuid();
        // RecordParticipationAsync seeds the affected record so the restoration guard passes.
        await sut.RecordParticipationAsync(clusterId, deviceId, null, ParticipationType.Affected, null, CancellationToken.None);
        await sut.RecordRestorationResponseAsync(clusterId, deviceId, null, "restored", CancellationToken.None);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.Equal(1, clusterRepo.ApplyTransitionCallCount);
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("possible_restoration", clusterRepo.Decisions[0].DecisionType);
        Assert.Single(clusterRepo.OutboxEvents);
    }

    // ── Path 4: Revert to Active (PossibleRestoration → Active) ─────────────

    [Fact]
    public async Task RevertToActive_RoutesThrough_ApplyClusterTransitionAsync()
    {
        var clusterId = Guid.NewGuid();
        var cluster = new SignalCluster
        {
            Id = clusterId,
            State = SignalState.PossibleRestoration,
            Category = CivicCategory.Roads,
            PossibleRestorationAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        var clusterRepo = new TrackingClusterRepo(cluster);
        // stillAffected (3) > restorationYes (1), stillAffected (3) >= min (2)
        var participRepo = new FakeParticipationRepo
        {
            RestorationSnapshot = new RestorationCountSnapshot(YesVotes: 1, NoVotes: 3, TotalResponses: 4)
        };
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, DefaultOpts);

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Null(cluster.PossibleRestorationAt);
        Assert.Equal(1, clusterRepo.ApplyTransitionCallCount);
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("revert_to_active", clusterRepo.Decisions[0].DecisionType);
        Assert.Single(clusterRepo.OutboxEvents);
        Assert.Equal(0, clusterRepo.StandaloneUpdateCallCount);
    }

    // ── Path 5: Resolve by Citizen Vote (PossibleRestoration → Resolved) ─────

    [Fact]
    public async Task ResolveByRestorationVote_RoutesThrough_ApplyClusterTransitionAsync()
    {
        var clusterId = Guid.NewGuid();
        var cluster = new SignalCluster
        {
            Id = clusterId,
            State = SignalState.PossibleRestoration,
            Category = CivicCategory.Roads,
            PossibleRestorationAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
        var clusterRepo = new TrackingClusterRepo(cluster);
        // ratio = 3/4 = 0.75 >= 0.60, total = 4 >= min 2
        var participRepo = new FakeParticipationRepo
        {
            RestorationSnapshot = new RestorationCountSnapshot(YesVotes: 3, NoVotes: 1, TotalResponses: 4)
        };
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, DefaultOpts);

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.Resolved, cluster.State);
        Assert.NotNull(cluster.ResolvedAt);
        Assert.Equal(1, clusterRepo.ApplyTransitionCallCount);
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("resolved", clusterRepo.Decisions[0].DecisionType);
        Assert.Single(clusterRepo.OutboxEvents);
        Assert.Equal(0, clusterRepo.StandaloneUpdateCallCount);
    }
}

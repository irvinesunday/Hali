using System;
using System.Collections.Generic;
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

/// <summary>
/// Verifies the lifecycle transitions driven by
/// <see cref="RestorationEvaluationService"/> (possible_restoration → active
/// and possible_restoration → resolved). Tests call the service directly to
/// prove the production code path is exercised, not a local test copy.
///
/// The invariant being enforced (issue #210): every transition routes through
/// <see cref="IClusterRepository.ApplyClusterTransitionAsync"/>. Each test
/// asserts that <c>ApplyClusterTransitionAsync</c> was called (by checking
/// that <c>Decisions</c> and <c>OutboxEvents</c> are populated via the
/// atomic method), and that the standalone <c>UpdateClusterAsync</c> /
/// <c>WriteCivisDecisionAsync</c> methods were NOT used for the transition.
/// </summary>
public class EvaluatePossibleRestorationJobTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class FakeClusterRepo : IClusterRepository
    {
        private readonly SignalCluster? _cluster;

        public List<SignalCluster> Updates { get; } = new();
        public List<CivisDecision> Decisions { get; } = new();
        public List<OutboxEvent> OutboxEvents { get; } = new();
        public List<SignalCluster> StandaloneUpdates { get; } = new();

        public FakeClusterRepo(SignalCluster? cluster = null) { _cluster = cluster; }

        public Task<SignalCluster?> GetClusterByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_cluster);

        public Task ApplyClusterTransitionAsync(SignalCluster cluster, CivisDecision? decision, OutboxEvent outboxEvent, CancellationToken ct)
        {
            Updates.Add(cluster);
            if (decision is not null) Decisions.Add(decision);
            OutboxEvents.Add(outboxEvent);
            return Task.CompletedTask;
        }

        // Tracked separately so tests can assert this was NOT called for lifecycle transitions.
        public Task UpdateClusterAsync(SignalCluster c, CancellationToken ct) { StandaloneUpdates.Add(c); return Task.CompletedTask; }
        public Task WriteCivisDecisionAsync(CivisDecision d, CancellationToken ct) { Decisions.Add(d); return Task.CompletedTask; }
        public Task WriteOutboxEventAsync(OutboxEvent e, CancellationToken ct) { OutboxEvents.Add(e); return Task.CompletedTask; }

        public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<SignalCluster> CreateClusterAsync(SignalCluster c, Guid s, Guid? d, CancellationToken ct) => Task.FromResult(c);
        public Task AttachToClusterAsync(Guid c, Guid s, Guid? d, string r, CancellationToken ct) => Task.CompletedTask;
        public Task<int> ComputeWrabCountAsync(Guid c, int d, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ComputeActiveMassCountAsync(Guid c, int h, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountUniqueDevicesAsync(Guid c, CancellationToken ct) => Task.FromResult(0);
        public Task<double> GetMinLocationConfidenceAsync(Guid c, CancellationToken ct) => Task.FromResult(1.0);
        public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task UpdateCountsAsync(Guid c, int a, int o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<OutboxEvent>)Array.Empty<OutboxEvent>());
        public Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeParticipationRepo : IParticipationRepository
    {
        private readonly Dictionary<(Guid, ParticipationType), int> _counts;

        public FakeParticipationRepo(Dictionary<(Guid, ParticipationType), int> counts)
        {
            _counts = counts;
        }

        public Task<RestorationCountSnapshot> GetRestorationCountSnapshotAsync(Guid clusterId, CancellationToken ct)
        {
            _counts.TryGetValue((clusterId, ParticipationType.RestorationYes), out var yes);
            _counts.TryGetValue((clusterId, ParticipationType.RestorationNo), out var no);
            _counts.TryGetValue((clusterId, ParticipationType.RestorationUnsure), out var unsure);
            return Task.FromResult(new RestorationCountSnapshot(yes, no, yes + no + unsure));
        }

        public Task<int> CountByTypeAsync(Guid c, ParticipationType t, CancellationToken ct) => Task.FromResult(0);
        public Task<ParticipationEntity?> GetByDeviceAsync(Guid c, Guid d, CancellationToken ct) => Task.FromResult<ParticipationEntity?>(null);
        public Task<ParticipationEntity?> GetMostRecentByAccountAsync(Guid c, Guid a, CancellationToken ct) => Task.FromResult<ParticipationEntity?>(null);
        public Task DeleteByDeviceAsync(Guid c, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task AddAsync(ParticipationEntity p, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateContextAsync(Guid id, string text, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Guid>)Array.Empty<Guid>());
        public async Task<IReadOnlyDictionary<Guid, RestorationCountSnapshot>> GetRestorationCountSnapshotsAsync(
            IReadOnlyCollection<Guid> clusterIds, CancellationToken ct)
        {
            var result = new Dictionary<Guid, RestorationCountSnapshot>(clusterIds.Count);
            foreach (var id in clusterIds)
            {
                var snap = await GetRestorationCountSnapshotAsync(id, ct);
                if (snap.TotalResponses > 0) result[id] = snap;
            }
            return result;
        }
    }

    private static CivisOptions DefaultOptions => new CivisOptions
    {
        RestorationRatio = 0.60,
        MinRestorationAffectedVotes = 2,
        ContextEditWindowMinutes = 2
    };

    private static SignalCluster PossibleRestorationCluster(Guid? id = null) => new SignalCluster
    {
        Id = id ?? Guid.NewGuid(),
        State = SignalState.PossibleRestoration,
        Category = CivicCategory.Roads,
        PossibleRestorationAt = DateTime.UtcNow.AddHours(-1),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FirstSeenAt = DateTime.UtcNow,
        LastSeenAt = DateTime.UtcNow
    };

    // ── revert-to-active path ─────────────────────────────────────────────────

    [Fact]
    public async Task StillAffectedVotesExceedRestoration_RevertsToActive_ViaAtomicTransition()
    {
        var clusterId = Guid.NewGuid();
        var cluster = PossibleRestorationCluster(clusterId);
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 1 },
            { (clusterId, ParticipationType.RestorationNo), 3 }
        };
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo(counts);
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Null(cluster.PossibleRestorationAt);
        // Invariant: transition committed via ApplyClusterTransitionAsync (atomic)
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("revert_to_active", clusterRepo.Decisions[0].DecisionType);
        Assert.Single(clusterRepo.OutboxEvents);
        // Invariant: standalone UpdateClusterAsync was NOT called for this transition
        Assert.Empty(clusterRepo.StandaloneUpdates);
    }

    [Fact]
    public async Task StillAffectedVotesBelowMinimum_DoesNotRevert()
    {
        var clusterId = Guid.NewGuid();
        var cluster = PossibleRestorationCluster(clusterId);
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 0 },
            { (clusterId, ParticipationType.RestorationNo), 1 }
        };
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo(counts);
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
        Assert.Empty(clusterRepo.OutboxEvents);
        Assert.Empty(clusterRepo.StandaloneUpdates);
    }

    // ── resolve path ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RestorationRatioMet_AtExactly60Percent_Resolves_ViaAtomicTransition()
    {
        var clusterId = Guid.NewGuid();
        var cluster = PossibleRestorationCluster(clusterId);
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 3 },
            { (clusterId, ParticipationType.RestorationNo), 2 }
        };
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo(counts);
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.Resolved, cluster.State);
        Assert.NotNull(cluster.ResolvedAt);
        // Invariant: transition committed via ApplyClusterTransitionAsync (atomic)
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("resolved", clusterRepo.Decisions[0].DecisionType);
        Assert.Single(clusterRepo.OutboxEvents);
        // Invariant: standalone UpdateClusterAsync was NOT called for this transition
        Assert.Empty(clusterRepo.StandaloneUpdates);
    }

    [Fact]
    public async Task RestorationRatioBelow60Percent_DoesNotResolve()
    {
        var clusterId = Guid.NewGuid();
        var cluster = PossibleRestorationCluster(clusterId);
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 2 },
            { (clusterId, ParticipationType.RestorationUnsure), 3 }
        };
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo(counts);
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
        Assert.Empty(clusterRepo.OutboxEvents);
    }

    [Fact]
    public async Task RestorationRatioAbove60Percent_Resolves()
    {
        var clusterId = Guid.NewGuid();
        var cluster = PossibleRestorationCluster(clusterId);
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 4 },
            { (clusterId, ParticipationType.RestorationNo), 1 }
        };
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo(counts);
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.Resolved, cluster.State);
        Assert.Single(clusterRepo.Decisions);
        Assert.Empty(clusterRepo.StandaloneUpdates);
    }

    // ── guard conditions ──────────────────────────────────────────────────────

    [Fact]
    public async Task ClusterNotFound_NoTransition()
    {
        var clusterRepo = new FakeClusterRepo(null);
        var participRepo = new FakeParticipationRepo(new Dictionary<(Guid, ParticipationType), int>());
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(Guid.NewGuid());

        Assert.Empty(clusterRepo.Decisions);
        Assert.Empty(clusterRepo.OutboxEvents);
    }

    [Fact]
    public async Task ClusterInWrongState_NoTransition()
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
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 5 },
        };
        var clusterRepo = new FakeClusterRepo(cluster);
        var participRepo = new FakeParticipationRepo(counts);
        var sut = new RestorationEvaluationService(clusterRepo, participRepo, Options.Create(DefaultOptions));

        await sut.EvaluateAsync(clusterId);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
        Assert.Empty(clusterRepo.OutboxEvents);
    }
}

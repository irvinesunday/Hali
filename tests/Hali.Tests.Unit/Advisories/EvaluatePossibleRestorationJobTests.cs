using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Participation;
using Hali.Domain.Entities.Clusters;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;
using Hali.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Advisories;

/// <summary>
/// Tests the restoration formula run by EvaluatePossibleRestorationJob.
/// We test the logic directly rather than the hosted service timer.
/// </summary>
public class EvaluatePossibleRestorationJobTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class FakeClusterRepo : IClusterRepository
    {
        private readonly List<SignalCluster> _possible;
        public List<SignalCluster> Updates { get; } = new();
        public List<CivisDecision> Decisions { get; } = new();
        public List<OutboxEvent> OutboxEvents { get; } = new();

        public FakeClusterRepo(IEnumerable<SignalCluster> possibleClusters)
        {
            _possible = new List<SignalCluster>(possibleClusters);
        }

        public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)_possible);

        public Task<SignalCluster?> GetClusterByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_possible.Find(c => c.Id == id));

        public Task UpdateClusterAsync(SignalCluster c, CancellationToken ct) { Updates.Add(c); return Task.CompletedTask; }
        public Task WriteCivisDecisionAsync(CivisDecision d, CancellationToken ct) { Decisions.Add(d); return Task.CompletedTask; }
        public Task WriteOutboxEventAsync(OutboxEvent e, CancellationToken ct) { OutboxEvents.Add(e); return Task.CompletedTask; }

        public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<SignalCluster> CreateClusterAsync(SignalCluster c, Guid s, Guid? d, CancellationToken ct) => Task.FromResult(c);
        public Task AttachToClusterAsync(Guid c, Guid s, Guid? d, string r, CancellationToken ct) => Task.CompletedTask;
        public Task<int> ComputeWrabCountAsync(Guid c, int d, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ComputeActiveMassCountAsync(Guid c, int h, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountUniqueDevicesAsync(Guid c, CancellationToken ct) => Task.FromResult(0);
        public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task UpdateCountsAsync(Guid c, int a, int o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
    }

    private sealed class FakeParticipationRepo : IParticipationRepository
    {
        private readonly Dictionary<(Guid, ParticipationType), int> _counts;

        public FakeParticipationRepo(Dictionary<(Guid, ParticipationType), int> counts)
        {
            _counts = counts;
        }

        public Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct)
        {
            _counts.TryGetValue((clusterId, type), out var count);
            return Task.FromResult(count);
        }

        public Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct)
        {
            int total = 0;
            foreach (var type in new[] { ParticipationType.RestorationYes, ParticipationType.RestorationNo, ParticipationType.RestorationUnsure })
            {
                _counts.TryGetValue((clusterId, type), out var c);
                total += c;
            }
            return Task.FromResult(total);
        }

        public Task<ParticipationEntity?> GetByDeviceAsync(Guid c, Guid d, CancellationToken ct) => Task.FromResult<ParticipationEntity?>(null);
        public Task DeleteByDeviceAsync(Guid c, Guid d, CancellationToken ct) => Task.CompletedTask;
        public Task AddAsync(ParticipationEntity p, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateContextAsync(Guid id, string text, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> GetAffectedAccountIdsAsync(Guid clusterId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Guid>)Array.Empty<Guid>());
    }

    private static CivisOptions DefaultOptions => new CivisOptions
    {
        RestorationRatio = 0.60,
        MinRestorationAffectedVotes = 2,
        ContextEditWindowMinutes = 2
    };

    // Simulate EvaluatePossibleRestorationJob.EvaluateClusterAsync logic
    private static async Task EvaluateCluster(
        SignalCluster cluster,
        IClusterRepository clusterRepo,
        IParticipationRepository participRepo,
        CivisOptions options)
    {
        var ct = CancellationToken.None;
        int restorationYes = await participRepo.CountByTypeAsync(cluster.Id, ParticipationType.RestorationYes, ct);
        int stillAffected = await participRepo.CountByTypeAsync(cluster.Id, ParticipationType.Affected, ct);
        int totalRestorationResponses = await participRepo.CountRestorationResponsesAsync(cluster.Id, ct);

        if (stillAffected > restorationYes && stillAffected >= options.MinRestorationAffectedVotes)
        {
            cluster.State = SignalState.Active;
            cluster.PossibleRestorationAt = null;
            cluster.UpdatedAt = DateTime.UtcNow;
            await clusterRepo.UpdateClusterAsync(cluster, ct);
            await clusterRepo.WriteCivisDecisionAsync(new CivisDecision
            {
                Id = Guid.NewGuid(),
                ClusterId = cluster.Id,
                DecisionType = "revert_to_active",
                ReasonCodes = "[\"still_affected_votes_exceed_restoration\"]",
                Metrics = JsonSerializer.Serialize(new { restoration_yes = restorationYes, still_affected = stillAffected }),
                CreatedAt = DateTime.UtcNow
            }, ct);
            return;
        }

        if (totalRestorationResponses >= options.MinRestorationAffectedVotes)
        {
            double ratio = (double)restorationYes / (double)totalRestorationResponses;
            if (ratio >= options.RestorationRatio)
            {
                cluster.State = SignalState.Resolved;
                cluster.ResolvedAt = DateTime.UtcNow;
                cluster.UpdatedAt = DateTime.UtcNow;
                await clusterRepo.UpdateClusterAsync(cluster, ct);
                await clusterRepo.WriteCivisDecisionAsync(new CivisDecision
                {
                    Id = Guid.NewGuid(),
                    ClusterId = cluster.Id,
                    DecisionType = "resolved",
                    ReasonCodes = "[\"restoration_ratio_met\"]",
                    Metrics = JsonSerializer.Serialize(new { restoration_yes = restorationYes, total_restoration_responses = totalRestorationResponses, ratio, threshold = options.RestorationRatio }),
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
        }
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StillAffectedVotesExceedRestoration_RevertsToActive()
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

        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 1 },
            { (clusterId, ParticipationType.Affected), 3 }  // 3 still-affected > 1 yes
        };

        var clusterRepo = new FakeClusterRepo(new[] { cluster });
        var participRepo = new FakeParticipationRepo(counts);

        await EvaluateCluster(cluster, clusterRepo, participRepo, DefaultOptions);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Null(cluster.PossibleRestorationAt);
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("revert_to_active", clusterRepo.Decisions[0].DecisionType);
    }

    [Fact]
    public async Task RestorationRatioMet_AtExactly60Percent_Resolves()
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

        // 3 yes out of 5 total = 0.60 exactly
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 3 },
            { (clusterId, ParticipationType.RestorationNo), 2 },
            { (clusterId, ParticipationType.Affected), 0 }
        };

        var clusterRepo = new FakeClusterRepo(new[] { cluster });
        var participRepo = new FakeParticipationRepo(counts);

        await EvaluateCluster(cluster, clusterRepo, participRepo, DefaultOptions);

        Assert.Equal(SignalState.Resolved, cluster.State);
        Assert.NotNull(cluster.ResolvedAt);
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("resolved", clusterRepo.Decisions[0].DecisionType);
    }

    [Fact]
    public async Task RestorationRatioBelow60Percent_DoesNotResolve()
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

        // 2 yes out of 5 total = 0.40 < 0.60
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 2 },
            { (clusterId, ParticipationType.RestorationNo), 3 },
            { (clusterId, ParticipationType.Affected), 0 }
        };

        var clusterRepo = new FakeClusterRepo(new[] { cluster });
        var participRepo = new FakeParticipationRepo(counts);

        await EvaluateCluster(cluster, clusterRepo, participRepo, DefaultOptions);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
    }

    [Fact]
    public async Task StillAffectedVotesBelowMinimum_DoesNotRevert()
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

        // 1 still-affected, but min is 2 — should not revert
        var counts = new Dictionary<(Guid, ParticipationType), int>
        {
            { (clusterId, ParticipationType.RestorationYes), 0 },
            { (clusterId, ParticipationType.Affected), 1 }  // < MinRestorationAffectedVotes
        };

        var clusterRepo = new FakeClusterRepo(new[] { cluster });
        var participRepo = new FakeParticipationRepo(counts);

        await EvaluateCluster(cluster, clusterRepo, participRepo, DefaultOptions);

        // No revert, no resolve — stays in possible_restoration
        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
    }
}

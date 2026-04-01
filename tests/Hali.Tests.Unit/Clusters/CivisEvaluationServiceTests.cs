using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hali.Tests.Unit.Clusters;

/// <summary>
/// Minimal fake repository to avoid NSubstitute argument-matching quirks.
/// </summary>
internal sealed class FakeClusterRepo : IClusterRepository
{
    private readonly SignalCluster? _cluster;
    public int WrabCount { get; set; }
    public int ActiveMass { get; set; }
    public int UniqueDevices { get; set; }

    public List<CivisDecision> Decisions { get; } = new();
    public List<OutboxEvent> OutboxEvents { get; } = new();
    public List<SignalCluster> Updates { get; } = new();

    public FakeClusterRepo(SignalCluster? cluster = null) => _cluster = cluster;

    public Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct)
        => Task.FromResult(_cluster);

    public Task<int> ComputeWrabCountAsync(Guid clusterId, int rollingWindowDays, CancellationToken ct)
        => Task.FromResult(WrabCount);

    public Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct)
        => Task.FromResult(ActiveMass);

    public Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct)
        => Task.FromResult(UniqueDevices);

    public Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct)
    {
        Updates.Add(cluster);
        return Task.CompletedTask;
    }

    public Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct)
    {
        Decisions.Add(decision);
        return Task.CompletedTask;
    }

    public Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct)
    {
        OutboxEvents.Add(outboxEvent);
        return Task.CompletedTask;
    }

    // Unused in these tests
    public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> spatialCells, CivicCategory category, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SignalCluster>>(Array.Empty<SignalCluster>());

    public Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid signalEventId, Guid? deviceId, CancellationToken ct)
        => Task.FromResult(cluster);

    public Task AttachToClusterAsync(Guid clusterId, Guid signalEventId, Guid? deviceId, string linkReason, CancellationToken ct)
        => Task.CompletedTask;

    public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SignalCluster>>(Array.Empty<SignalCluster>());
}

/// <summary>
/// Tests for CivisEvaluationService — activation gate, device diversity, state transitions.
/// </summary>
public class CivisEvaluationServiceTests
{
    private static CivisOptions DefaultOptions() => new()
    {
        WrabRollingWindowDays = 30,
        JoinThreshold = 0.65,
        MinUniqueDevices = 2,
        DeactivationThreshold = 0.5,
        ActiveMassHorizonHours = 48,
        TimeScoreMaxAgeHours = 24,
        Roads = new CivisCategoryOptions { BaseFloor = 2, HalfLifeHours = 18, MacfMin = 2, MacfMax = 6 }
    };

    private static SignalCluster UnconfirmedRoadsCluster(int rawCount = 1) => new()
    {
        Id = Guid.NewGuid(),
        Category = CivicCategory.Roads,
        State = SignalState.Unconfirmed,
        RawConfirmationCount = rawCount,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FirstSeenAt = DateTime.UtcNow,
        LastSeenAt = DateTime.UtcNow
    };

    private static (CivisEvaluationService svc, FakeClusterRepo repo) Build(
        SignalCluster cluster,
        int wrabCount = 0,
        int activeMass = 0,
        int uniqueDevices = 0,
        CivisOptions? opts = null)
    {
        var repo = new FakeClusterRepo(cluster)
        {
            WrabCount = wrabCount,
            ActiveMass = activeMass,
            UniqueDevices = uniqueDevices
        };
        var svc = new CivisEvaluationService(repo, Options.Create(opts ?? DefaultOptions()));
        return (svc, repo);
    }

    // -------------------------------------------------------------------------
    // Activation gate — pass
    //
    // wrabCount=3, activeMass=3 → effective_WRAB=3, SDS=1.0
    // MACF = ceil(2 + log2(2)) = 3
    // rawCount=3 >= MACF=3 AND uniqueDevices=2 >= 2 → activates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateCluster_WhenMacfMetAndDevicesMet_TransitionsToActive()
    {
        var cluster = UnconfirmedRoadsCluster(rawCount: 3);
        var (svc, _) = Build(cluster, wrabCount: 3, activeMass: 3, uniqueDevices: 2);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.NotNull(cluster.ActivatedAt);
    }

    [Fact]
    public async Task EvaluateCluster_WhenMacfMetAndDevicesMet_WritesCivisDecision()
    {
        var cluster = UnconfirmedRoadsCluster(rawCount: 3);
        var (svc, repo) = Build(cluster, wrabCount: 3, activeMass: 3, uniqueDevices: 2);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Single(repo.Decisions);
        Assert.Equal("activated", repo.Decisions[0].DecisionType);
    }

    [Fact]
    public async Task EvaluateCluster_WhenMacfMetAndDevicesMet_EmitsOutboxEvent()
    {
        var cluster = UnconfirmedRoadsCluster(rawCount: 3);
        var (svc, repo) = Build(cluster, wrabCount: 3, activeMass: 3, uniqueDevices: 2);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Single(repo.OutboxEvents);
        Assert.Equal("cluster_state_changed", repo.OutboxEvents[0].EventType);
    }

    // -------------------------------------------------------------------------
    // Activation gate — fail: MACF not met
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateCluster_WhenMacfNotMet_StaysUnconfirmed()
    {
        // MACF=3 at SDS=1; rawCount=2 < 3
        var cluster = UnconfirmedRoadsCluster(rawCount: 2);
        var (svc, _) = Build(cluster, wrabCount: 3, activeMass: 3, uniqueDevices: 2);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Unconfirmed, cluster.State);
        Assert.Null(cluster.ActivatedAt);
    }

    [Fact]
    public async Task EvaluateCluster_WhenMacfNotMet_DoesNotWriteCivisDecision()
    {
        var cluster = UnconfirmedRoadsCluster(rawCount: 2);
        var (svc, repo) = Build(cluster, wrabCount: 3, activeMass: 3, uniqueDevices: 2);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Empty(repo.Decisions);
    }

    // -------------------------------------------------------------------------
    // Device diversity gate
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateCluster_WhenUniqueDevicesBelowMinimum_StaysUnconfirmed()
    {
        // rawCount=5 satisfies MACF, but only 1 unique device
        var cluster = UnconfirmedRoadsCluster(rawCount: 5);
        var (svc, _) = Build(cluster, wrabCount: 5, activeMass: 5, uniqueDevices: 1);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Unconfirmed, cluster.State);
    }

    [Fact]
    public async Task EvaluateCluster_WhenTwoDistinctDevices_ActivationSucceeds()
    {
        var cluster = UnconfirmedRoadsCluster(rawCount: 3);
        var (svc, _) = Build(cluster, wrabCount: 3, activeMass: 3, uniqueDevices: 2);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Active, cluster.State);
    }

    [Fact]
    public async Task EvaluateCluster_WhenSameDeviceRepeated_CountsAsOneDevice()
    {
        // Repo returns 1 unique device even with 10 links from same device
        var cluster = UnconfirmedRoadsCluster(rawCount: 10);
        var (svc, _) = Build(cluster, wrabCount: 10, activeMass: 10, uniqueDevices: 1);

        await svc.EvaluateClusterAsync(cluster.Id);

        Assert.Equal(SignalState.Unconfirmed, cluster.State);
    }

    // -------------------------------------------------------------------------
    // Skips non-unconfirmed clusters
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateCluster_WhenAlreadyActive_DoesNotQueryWrab()
    {
        var cluster = UnconfirmedRoadsCluster(rawCount: 5);
        cluster.State = SignalState.Active;
        var (svc, repo) = Build(cluster, wrabCount: 5, activeMass: 5, uniqueDevices: 3);

        await svc.EvaluateClusterAsync(cluster.Id);

        // Service returns early — no updates written
        Assert.Empty(repo.Updates);
    }

    // -------------------------------------------------------------------------
    // ApplyDecayAsync: Active → PossibleRestoration → Resolved
    // -------------------------------------------------------------------------

    private static (CivisEvaluationService svc, FakeClusterRepo repo) BuildDecay(
        SignalCluster cluster, CivisOptions? opts = null)
    {
        var repo = new FakeClusterRepo(cluster);
        var svc = new CivisEvaluationService(repo, Options.Create(opts ?? DefaultOptions()));
        return (svc, repo);
    }

    private static SignalCluster ActiveRoadsCluster(int rawCount = 4) => new()
    {
        Id = Guid.NewGuid(),
        Category = CivicCategory.Roads,
        State = SignalState.Active,
        RawConfirmationCount = rawCount,
        Wrab = 2m,
        // Last seen 100h ago → ~5.5 half-lives for roads (half_life=18h) → ~3% remaining
        CreatedAt = DateTime.UtcNow.AddHours(-100),
        UpdatedAt = DateTime.UtcNow.AddHours(-100),
        FirstSeenAt = DateTime.UtcNow.AddHours(-100),
        LastSeenAt = DateTime.UtcNow.AddHours(-100),
        ActivatedAt = DateTime.UtcNow.AddHours(-100)
    };

    [Fact]
    public async Task ApplyDecay_WhenLiveMassBelowThreshold_ActiveTransitionsToPossibleRestoration()
    {
        var cluster = ActiveRoadsCluster(rawCount: 4);
        var (svc, _) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.NotNull(cluster.PossibleRestorationAt);
    }

    [Fact]
    public async Task ApplyDecay_WhenLiveMassBelowThreshold_WritesCivisDecision()
    {
        var cluster = ActiveRoadsCluster(rawCount: 4);
        var (svc, repo) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Single(repo.Decisions);
        Assert.Equal("possible_restoration", repo.Decisions[0].DecisionType);
    }

    [Fact]
    public async Task ApplyDecay_WhenPossibleRestorationAndStillBelowThreshold_TransitionsToResolved()
    {
        var cluster = ActiveRoadsCluster(rawCount: 4);
        cluster.State = SignalState.PossibleRestoration;
        cluster.PossibleRestorationAt = DateTime.UtcNow.AddHours(-100);
        var (svc, _) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Equal(SignalState.Resolved, cluster.State);
        Assert.NotNull(cluster.ResolvedAt);
    }

    [Fact]
    public async Task ApplyDecay_WhenPossibleRestorationResolved_WritesCivisDecisionWithResolvedByDecay()
    {
        var cluster = ActiveRoadsCluster(rawCount: 4);
        cluster.State = SignalState.PossibleRestoration;
        cluster.PossibleRestorationAt = DateTime.UtcNow.AddHours(-100);
        var (svc, repo) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Single(repo.Decisions);
        Assert.Equal("resolved_by_decay", repo.Decisions[0].DecisionType);
    }

    [Fact]
    public async Task ApplyDecay_WhenLiveMassAboveThreshold_NoStateChange()
    {
        var cluster = new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = CivicCategory.Roads,
            State = SignalState.Active,
            RawConfirmationCount = 100,
            Wrab = 2m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow
        };
        var (svc, repo) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(repo.Decisions);
    }

    [Fact]
    public async Task ApplyDecay_WhenClusterAlreadyResolved_DoesNothing()
    {
        var cluster = ActiveRoadsCluster();
        cluster.State = SignalState.Resolved;
        var (svc, repo) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Empty(repo.Updates);
    }

    [Fact]
    public async Task ApplyDecay_StateTransitions_EmitsOutboxEvent()
    {
        var cluster = ActiveRoadsCluster(rawCount: 4);
        var (svc, repo) = BuildDecay(cluster);

        await svc.ApplyDecayAsync(cluster.Id);

        Assert.Single(repo.OutboxEvents);
        Assert.Equal("cluster_state_changed", repo.OutboxEvents[0].EventType);
    }
}

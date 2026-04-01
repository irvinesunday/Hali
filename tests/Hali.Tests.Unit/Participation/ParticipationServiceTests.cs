using Hali.Application.Clusters;
using Hali.Application.Participation;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;

namespace Hali.Tests.Unit.Participation;

// ---------------------------------------------------------------------------
// Minimal in-memory participation repo
// ---------------------------------------------------------------------------
internal sealed class FakeParticipationRepo : IParticipationRepository
{
    private readonly List<ParticipationEntity> _store = new();

    public Task<ParticipationEntity?> GetByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
    {
        var p = _store
            .Where(x => x.ClusterId == clusterId && x.DeviceId == deviceId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(p);
    }

    public Task DeleteByDeviceAsync(Guid clusterId, Guid deviceId, CancellationToken ct)
    {
        _store.RemoveAll(x => x.ClusterId == clusterId && x.DeviceId == deviceId);
        return Task.CompletedTask;
    }

    public Task AddAsync(ParticipationEntity participation, CancellationToken ct)
    {
        _store.Add(participation);
        return Task.CompletedTask;
    }

    public Task UpdateContextAsync(Guid participationId, string contextText, CancellationToken ct)
    {
        var p = _store.FirstOrDefault(x => x.Id == participationId);
        if (p is not null) p.ContextText = contextText;
        return Task.CompletedTask;
    }

    public Task<int> CountByTypeAsync(Guid clusterId, ParticipationType type, CancellationToken ct)
        => Task.FromResult(_store.Count(x => x.ClusterId == clusterId && x.ParticipationType == type));

    public Task<int> CountRestorationResponsesAsync(Guid clusterId, CancellationToken ct)
        => Task.FromResult(_store.Count(x => x.ClusterId == clusterId
            && (x.ParticipationType == ParticipationType.RestorationYes
                || x.ParticipationType == ParticipationType.RestorationNo
                || x.ParticipationType == ParticipationType.RestorationUnsure)));

    public List<ParticipationEntity> All => _store;
}

// ---------------------------------------------------------------------------
// Minimal cluster repo stub for participation tests
// ---------------------------------------------------------------------------
internal sealed class FakeClusterRepoForParticipation : IClusterRepository
{
    private readonly SignalCluster? _cluster;
    public List<(int Affected, int Observing)> CountUpdates { get; } = new();
    public List<SignalCluster> ClusterUpdates { get; } = new();
    public List<CivisDecision> Decisions { get; } = new();
    public List<OutboxEvent> OutboxEvents { get; } = new();

    public FakeClusterRepoForParticipation(SignalCluster? cluster = null) => _cluster = cluster;

    public Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct)
        => Task.FromResult(_cluster);

    public Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct)
    {
        CountUpdates.Add((affectedCount, observingCount));
        if (_cluster is not null)
        {
            _cluster.AffectedCount = affectedCount;
            _cluster.ObservingCount = observingCount;
        }
        return Task.CompletedTask;
    }

    public Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct)
    {
        ClusterUpdates.Add(cluster);
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

    // Unused
    public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SignalCluster>>(Array.Empty<SignalCluster>());
    public Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid seId, Guid? dId, CancellationToken ct)
        => Task.FromResult(cluster);
    public Task AttachToClusterAsync(Guid cId, Guid seId, Guid? dId, string r, CancellationToken ct)
        => Task.CompletedTask;
    public Task<int> ComputeWrabCountAsync(Guid cId, int d, CancellationToken ct) => Task.FromResult(0);
    public Task<int> ComputeActiveMassCountAsync(Guid cId, int h, CancellationToken ct) => Task.FromResult(0);
    public Task<int> CountUniqueDevicesAsync(Guid cId, CancellationToken ct) => Task.FromResult(0);
    public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SignalCluster>>(Array.Empty<SignalCluster>());
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
public class ParticipationServiceTests
{
    private static readonly Guid ClusterId = Guid.NewGuid();
    private static readonly Guid DeviceA = Guid.NewGuid();
    private static readonly Guid DeviceB = Guid.NewGuid();

    private static CivisOptions DefaultOptions() => new()
    {
        ContextEditWindowMinutes = 2,
        RestorationRatio = 0.60,
        MinRestorationAffectedVotes = 1
    };

    private static SignalCluster ActiveCluster() => new()
    {
        Id = ClusterId,
        Category = CivicCategory.Roads,
        State = SignalState.Active,
        RawConfirmationCount = 3,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        FirstSeenAt = DateTime.UtcNow,
        LastSeenAt = DateTime.UtcNow,
        ActivatedAt = DateTime.UtcNow
    };

    private static (ParticipationService svc, FakeParticipationRepo pRepo, FakeClusterRepoForParticipation cRepo) Build(
        SignalCluster? cluster = null,
        CivisOptions? opts = null)
    {
        var pRepo = new FakeParticipationRepo();
        var cRepo = new FakeClusterRepoForParticipation(cluster);
        var svc = new ParticipationService(pRepo, cRepo, Options.Create(opts ?? DefaultOptions()));
        return (svc, pRepo, cRepo);
    }

    // -----------------------------------------------------------------------
    // One-type-per-device enforcement
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecordParticipation_WhenDeviceAlreadyParticipated_ReplacesExisting()
    {
        var (svc, pRepo, _) = Build();
        await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default);
        await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default);

        var all = pRepo.All.Where(p => p.ClusterId == ClusterId && p.DeviceId == DeviceA).ToList();
        Assert.Single(all);
        Assert.Equal(ParticipationType.Affected, all[0].ParticipationType);
    }

    [Fact]
    public async Task RecordParticipation_TwoDevices_BothStoredIndependently()
    {
        var (svc, pRepo, _) = Build();
        await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default);
        await svc.RecordParticipationAsync(ClusterId, DeviceB, null, ParticipationType.Observing, null, default);

        Assert.Equal(2, pRepo.All.Count);
    }

    [Fact]
    public async Task RecordParticipation_UpdatesClusterCounts()
    {
        var cluster = ActiveCluster();
        var (svc, _, cRepo) = Build(cluster);
        await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default);
        await svc.RecordParticipationAsync(ClusterId, DeviceB, null, ParticipationType.Observing, null, default);

        Assert.Equal(1, cluster.AffectedCount);
        Assert.Equal(1, cluster.ObservingCount);
    }

    // -----------------------------------------------------------------------
    // Context gate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddContext_WithoutAffectedParticipation_ThrowsContextRequiresAffected()
    {
        var (svc, _, _) = Build();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddContextAsync(ClusterId, DeviceA, "Test context", default));
        Assert.Equal("CONTEXT_REQUIRES_AFFECTED", ex.Message);
    }

    [Fact]
    public async Task AddContext_WithObservingParticipation_ThrowsContextRequiresAffected()
    {
        var (svc, _, _) = Build();
        await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddContextAsync(ClusterId, DeviceA, "Test context", default));
        Assert.Equal("CONTEXT_REQUIRES_AFFECTED", ex.Message);
    }

    [Fact]
    public async Task AddContext_WithAffectedParticipationWithinWindow_Succeeds()
    {
        var (svc, pRepo, _) = Build();
        await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default);
        await svc.AddContextAsync(ClusterId, DeviceA, "Big pothole on main road", default);

        var p = pRepo.All.Single(x => x.DeviceId == DeviceA);
        Assert.Equal("Big pothole on main road", p.ContextText);
    }

    [Fact]
    public async Task AddContext_AfterWindowExpired_ThrowsContextEditWindowExpired()
    {
        var (svc, pRepo, _) = Build();
        // Manually add an affected participation with an old timestamp
        pRepo.All.Add(new ParticipationEntity
        {
            Id = Guid.NewGuid(),
            ClusterId = ClusterId,
            DeviceId = DeviceA,
            ParticipationType = ParticipationType.Affected,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5) // 5 minutes ago — beyond 2-min window
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AddContextAsync(ClusterId, DeviceA, "Late context", default));
        Assert.Equal("CONTEXT_EDIT_WINDOW_EXPIRED", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Restoration response
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RecordRestorationResponse_Restored_MapsToRestorationYes()
    {
        var cluster = ActiveCluster();
        var (svc, pRepo, _) = Build(cluster);
        await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default);

        var p = pRepo.All.Single(x => x.DeviceId == DeviceA);
        Assert.Equal(ParticipationType.RestorationYes, p.ParticipationType);
    }

    [Fact]
    public async Task RecordRestorationResponse_StillAffected_MapsToAffected()
    {
        var cluster = ActiveCluster();
        var (svc, pRepo, _) = Build(cluster);
        await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "still_affected", default);

        var p = pRepo.All.Single(x => x.DeviceId == DeviceA);
        Assert.Equal(ParticipationType.Affected, p.ParticipationType);
    }

    [Fact]
    public async Task RecordRestorationResponse_NotSure_MapsToRestorationUnsure()
    {
        var cluster = ActiveCluster();
        var (svc, pRepo, _) = Build(cluster);
        await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "not_sure", default);

        var p = pRepo.All.Single(x => x.DeviceId == DeviceA);
        Assert.Equal(ParticipationType.RestorationUnsure, p.ParticipationType);
    }

    [Fact]
    public async Task RecordRestorationResponse_RestoredWithRatioMet_TransitionsClusterToPossibleRestoration()
    {
        var cluster = ActiveCluster();
        var (svc, _, cRepo) = Build(cluster);
        await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.NotNull(cluster.PossibleRestorationAt);
    }

    [Fact]
    public async Task RecordRestorationResponse_RestoredRatioMet_WritesDecisionAndOutboxEvent()
    {
        var cluster = ActiveCluster();
        var (svc, _, cRepo) = Build(cluster);
        await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default);

        Assert.Single(cRepo.Decisions);
        Assert.Equal("possible_restoration", cRepo.Decisions[0].DecisionType);
        Assert.Single(cRepo.OutboxEvents);
    }

    [Fact]
    public async Task RecordRestorationResponse_RestoredRatioNotMet_ClusterRemainsActive()
    {
        var cluster = ActiveCluster();
        var opts = new CivisOptions
        {
            ContextEditWindowMinutes = 2,
            RestorationRatio = 0.60,
            MinRestorationAffectedVotes = 3 // requires 3 votes
        };
        var (svc, _, cRepo) = Build(cluster, opts);
        await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default);

        // Only 1 vote, min is 3 → no transition
        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(cRepo.Decisions);
    }
}

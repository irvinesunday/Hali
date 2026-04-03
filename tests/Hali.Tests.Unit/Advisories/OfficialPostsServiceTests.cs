using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Clusters;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Xunit;

namespace Hali.Tests.Unit.Advisories;

public class OfficialPostsServiceTests
{
    // ── fake repo ────────────────────────────────────────────────────────────

    private sealed class FakeOfficialPostRepo : IOfficialPostRepository
    {
        public bool IntersectsResult { get; set; } = true;
        public OfficialPost? Created { get; private set; }

        public Task<OfficialPost> CreateAsync(OfficialPost post, OfficialPostScope scope, CancellationToken ct)
        {
            Created = post;
            return Task.FromResult(post);
        }

        public Task<bool> JurisdictionIntersectsScopeAsync(Guid institutionId, Guid postId, CancellationToken ct)
            => Task.FromResult(IntersectsResult);

        public Task<List<OfficialPost>> GetByClusterIdAsync(Guid clusterId, CancellationToken ct)
            => Task.FromResult(new List<OfficialPost>());

        public Task<List<OfficialPost>> GetActiveByLocalityAsync(Guid localityId, CancellationToken ct)
            => Task.FromResult(new List<OfficialPost>());

        public Task<int> ExpirePostsAsync(CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakeClusterRepo : IClusterRepository
    {
        private SignalCluster? _cluster;
        public List<SignalCluster> Updates { get; } = new();
        public List<CivisDecision> Decisions { get; } = new();
        public List<OutboxEvent> OutboxEvents { get; } = new();

        public FakeClusterRepo(SignalCluster? cluster = null) { _cluster = cluster; }

        public Task<SignalCluster?> GetClusterByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_cluster);

        public Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct)
        { Updates.Add(cluster); return Task.CompletedTask; }

        public Task WriteCivisDecisionAsync(CivisDecision d, CancellationToken ct)
        { Decisions.Add(d); return Task.CompletedTask; }

        public Task WriteOutboxEventAsync(OutboxEvent e, CancellationToken ct)
        { OutboxEvents.Add(e); return Task.CompletedTask; }

        public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());

        public Task<SignalCluster> CreateClusterAsync(SignalCluster c, Guid seId, Guid? dId, CancellationToken ct)
            => Task.FromResult(c);

        public Task AttachToClusterAsync(Guid c, Guid se, Guid? d, string r, CancellationToken ct)
            => Task.CompletedTask;

        public Task<int> ComputeWrabCountAsync(Guid c, int d, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ComputeActiveMassCountAsync(Guid c, int h, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountUniqueDevicesAsync(Guid c, CancellationToken ct) => Task.FromResult(0);

        public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());

        public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());

        public Task UpdateCountsAsync(Guid c, int a, int o, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct) => Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static CreateOfficialPostRequestDto ValidDto(string type = "live_update", bool isRestoration = false, Guid? clusterId = null) =>
        new CreateOfficialPostRequestDto
        {
            Type = type,
            Category = "roads",
            Title = "Road works notice",
            Body = "Night works on Main St.",
            IsRestorationClaim = isRestoration,
            RelatedClusterId = clusterId,
            LocalityId = Guid.NewGuid()
        };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePost_WhenJurisdictionIntersects_ReturnsCreatedPost()
    {
        var repo = new FakeOfficialPostRepo { IntersectsResult = true };
        var svc = new OfficialPostsService(repo, new FakeClusterRepo());

        var result = await svc.CreatePostAsync(Guid.NewGuid(), null, ValidDto(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("live_update", result.Type);
        Assert.Equal("published", result.Status);
    }

    [Fact]
    public async Task CreatePost_WhenOutsideJurisdiction_ThrowsOutsideJurisdiction()
    {
        var repo = new FakeOfficialPostRepo { IntersectsResult = false };
        var svc = new OfficialPostsService(repo, new FakeClusterRepo());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CreatePostAsync(Guid.NewGuid(), null, ValidDto(), CancellationToken.None));

        Assert.Equal("OUTSIDE_JURISDICTION", ex.Message);
    }

    [Fact]
    public async Task CreatePost_LiveUpdateWithRestorationClaim_TransitionsActiveClusterToPossibleRestoration()
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

        var repo = new FakeOfficialPostRepo { IntersectsResult = true };
        var clusterRepo = new FakeClusterRepo(cluster);
        var svc = new OfficialPostsService(repo, clusterRepo);

        await svc.CreatePostAsync(Guid.NewGuid(), null, ValidDto("live_update", isRestoration: true, clusterId: clusterId), CancellationToken.None);

        Assert.Equal(SignalState.PossibleRestoration, cluster.State);
        Assert.NotNull(cluster.PossibleRestorationAt);
        Assert.Single(clusterRepo.Decisions);
        Assert.Equal("possible_restoration", clusterRepo.Decisions[0].DecisionType);
    }

    [Fact]
    public async Task CreatePost_LiveUpdateWithoutRestorationClaim_DoesNotChangeClusterState()
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

        var repo = new FakeOfficialPostRepo { IntersectsResult = true };
        var clusterRepo = new FakeClusterRepo(cluster);
        var svc = new OfficialPostsService(repo, clusterRepo);

        await svc.CreatePostAsync(Guid.NewGuid(), null, ValidDto("live_update", isRestoration: false, clusterId: clusterId), CancellationToken.None);

        Assert.Equal(SignalState.Active, cluster.State);
        Assert.Empty(clusterRepo.Decisions);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Clusters;

public class ClusteringServiceTests
{
    private static CivisOptions DefaultOptions() => new CivisOptions
    {
        JoinThreshold = 0.65,
        TimeScoreMaxAgeHours = 24.0
    };

    private static (ClusteringService svc, IClusterRepository repo, IH3CellService h3, ICivisEvaluationService civis)
        Build(CivisOptions? opts = null)
    {
        var repo = Substitute.For<IClusterRepository>();
        var h3 = Substitute.For<IH3CellService>();
        var civis = Substitute.For<ICivisEvaluationService>();
        var options = Options.Create(opts ?? DefaultOptions());
        var svc = new ClusteringService(repo, h3, civis, options);
        return (svc, repo, h3, civis);
    }

    private static readonly Guid DefaultLocalityId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static SignalEvent MakeSignal(string? spatialCellId = "8928308280fffff", CivicCategory category = CivicCategory.Water, Guid? localityId = null)
    {
        return new SignalEvent
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            Category = category,
            SubcategorySlug = "low-pressure",
            ConditionSlug = "no-water",
            NeutralSummary = "No water in the area.",
            TemporalType = "episodic",
            SpatialCellId = spatialCellId,
            LocalityId = localityId,
            OccurredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static SignalCluster MakeCluster(
        string spatialCellId = "8928308280fffff",
        CivicCategory category = CivicCategory.Water,
        string? conditionSlug = "no-water",
        int rawConfirmationCount = 1,
        double lastSeenAgoHours = 0.5)
    {
        return new SignalCluster
        {
            Id = Guid.NewGuid(),
            Category = category,
            State = SignalState.Unconfirmed,
            SpatialCellId = spatialCellId,
            DominantConditionSlug = conditionSlug,
            RawConfirmationCount = rawConfirmationCount,
            CreatedAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            UpdatedAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            FirstSeenAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours),
            LastSeenAt = DateTime.UtcNow.AddHours(-lastSeenAgoHours)
        };
    }

    // -----------------------------------------------------------------------
    // Guard: no SpatialCellId → early return, nothing is called
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenSpatialCellIdIsNull_Throws()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal(spatialCellId: null);

        // H3 (#153): retyped from InvalidOperationException to a typed
        // InvariantViolationException carrying ErrorCodes.ClusteringNoSpatialCell.
        // Wire behaviour is unchanged (redacted to server.internal_error by
        // ExceptionToApiErrorMapper for ErrorCategory.Unexpected).
        var ex = await Assert.ThrowsAsync<InvariantViolationException>(() => svc.RouteSignalAsync(signal));
        Assert.Equal(ErrorCodes.ClusteringNoSpatialCell, ex.Code);

        h3.DidNotReceive().GetKRingCells(Arg.Any<string>(), Arg.Any<int>());
        await repo.DidNotReceive().FindCandidateClustersAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Join path: matching cluster above threshold
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenMatchingClusterFound_AttachesSignalToCluster()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        // Same cell + matching condition + recent → score will be well above 0.65
        var existingCluster = MakeCluster(lastSeenAgoHours: 0.5);

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        var result = await svc.RouteSignalAsync(signal);

        await repo.Received(1).AttachToClusterAsync(existingCluster.Id, signal.Id, signal.DeviceId, "join", Arg.Any<CancellationToken>());
        Assert.Equal(existingCluster.Id, result.ClusterId);
        Assert.True(result.WasJoined);
        Assert.False(result.WasCreated);
    }

    [Fact]
    public async Task RouteSignal_WhenMatchingClusterFound_UpdatesClusterAndIncrementsRawCount()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        var existingCluster = MakeCluster(rawConfirmationCount: 3, lastSeenAgoHours: 0.5);

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        await svc.RouteSignalAsync(signal);

        // RawConfirmationCount incremented in-memory before UpdateClusterAsync is called
        Assert.Equal(4, existingCluster.RawConfirmationCount);
        await repo.Received(1).UpdateClusterAsync(existingCluster, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteSignal_WhenMatchingClusterFound_WritesOutboxEventWithJoinType()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        var existingCluster = MakeCluster(lastSeenAgoHours: 0.5);

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        await svc.RouteSignalAsync(signal);

        await repo.Received(1).WriteOutboxEventAsync(
            Arg.Is<OutboxEvent>(e =>
                e.AggregateType == "signal_cluster" &&
                e.AggregateId == existingCluster.Id &&
                e.EventType == "cluster.updated" &&
                e.SchemaVersion == "1.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteSignal_WhenMatchingClusterFound_TriggersCivisEvaluation()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        var existingCluster = MakeCluster(lastSeenAgoHours: 0.5);

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        await svc.RouteSignalAsync(signal);

        await civis.Received(1).EvaluateClusterAsync(existingCluster.Id, Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Create path: no candidate above threshold
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenNoCandidateCluster_CreatesNewUnconfirmedCluster()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        // Capture what was passed to CreateClusterAsync
        SignalCluster? created = null;
        repo.CreateClusterAsync(Arg.Do<SignalCluster>(c => created = c), signal.Id, signal.DeviceId, Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        var result = await svc.RouteSignalAsync(signal);

        await repo.Received(1).CreateClusterAsync(
            Arg.Any<SignalCluster>(), signal.Id, signal.DeviceId, Arg.Any<CancellationToken>());
        Assert.NotNull(created);
        Assert.Equal(SignalState.Unconfirmed, created!.State);
        Assert.Equal(signal.Category, created.Category);
        Assert.Equal(1, created.RawConfirmationCount);
        Assert.Equal(created.Id, result.ClusterId);
        Assert.True(result.WasCreated);
        Assert.False(result.WasJoined);
        Assert.Equal("unconfirmed", result.ClusterState);
    }

    [Fact]
    public async Task RouteSignal_WhenNoCandidateCluster_WritesOutboxEventWithCreatedType()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        await repo.Received(1).WriteOutboxEventAsync(
            Arg.Is<OutboxEvent>(e =>
                e.AggregateType == "signal_cluster" &&
                e.EventType == "cluster.created" &&
                e.SchemaVersion == "1.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteSignal_WhenNoCandidateCluster_TriggersCivisEvaluation()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        await civis.Received(1).EvaluateClusterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Join score: candidate below threshold is not joined
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenCandidateScoreBelowThreshold_CreatesNewClusterInstead()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal(category: CivicCategory.Roads);
        signal.ConditionSlug = "pothole";

        // Stale cluster: 25 hours old → time score = 0, different condition → score = 0.4 + 0.25*1 + 0 + 0 = 0.65
        // Use a different cell so distance score is 0.75 instead of 1.0
        // and an old timestamp so time score approaches 0
        // score = 0.4 + 0.25*0.75 + 0.2*0 + 0.15*0 = 0.4 + 0.1875 = 0.5875 < 0.65
        var staleCluster = MakeCluster(
            spatialCellId: "891f1d48177ffff",   // different cell → distance factor = 0.75
            category: CivicCategory.Roads,
            conditionSlug: "flooding",           // different condition → condition score = 0
            lastSeenAgoHours: 25.0);            // beyond 24h max → time score = 0

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId!, staleCluster.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { staleCluster });
        repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        // Should have created a new cluster, not attached to the stale one
        await repo.Received(1).CreateClusterAsync(
            Arg.Any<SignalCluster>(), signal.Id, signal.DeviceId, Arg.Any<CancellationToken>());
        await repo.DidNotReceive().AttachToClusterAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Join score: when multiple candidates, best score wins
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenMultipleCandidates_AttachesToHighestScoringCluster()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();

        // Good cluster: same cell, same condition, very recent → high score
        var goodCluster = MakeCluster(lastSeenAgoHours: 0.1);
        // Weaker cluster: same cell, same condition, 10 hours old → lower time component
        var weakCluster = MakeCluster(lastSeenAgoHours: 10.0);
        weakCluster.Id = Guid.NewGuid(); // ensure distinct ID

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { weakCluster, goodCluster });

        await svc.RouteSignalAsync(signal);

        // Must attach to the best (most recent) cluster
        await repo.Received(1).AttachToClusterAsync(goodCluster.Id, signal.Id, signal.DeviceId, "join", Arg.Any<CancellationToken>());
        await repo.DidNotReceive().AttachToClusterAsync(weakCluster.Id, Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Phase A2: Locality propagation and consistency
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenNewClusterCreated_InheritsLocalityFromSignal()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal(localityId: DefaultLocalityId);

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        SignalCluster? created = null;
        repo.CreateClusterAsync(Arg.Do<SignalCluster>(c => created = c), signal.Id, signal.DeviceId, Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        Assert.NotNull(created);
        Assert.Equal(DefaultLocalityId, created!.LocalityId);
    }

    [Fact]
    public async Task RouteSignal_WhenCandidateHasDifferentLocality_SkipsAndCreatesNew()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal(localityId: DefaultLocalityId);
        var differentLocalityId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var mismatchCluster = MakeCluster(lastSeenAgoHours: 0.1);
        mismatchCluster.LocalityId = differentLocalityId;

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { mismatchCluster });
        repo.CreateClusterAsync(Arg.Any<SignalCluster>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        // Should NOT attach to the mismatched cluster
        await repo.DidNotReceive().AttachToClusterAsync(
            mismatchCluster.Id, Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // Should create a new cluster instead
        await repo.Received(1).CreateClusterAsync(
            Arg.Any<SignalCluster>(), signal.Id, signal.DeviceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteSignal_WhenCandidateHasSameLocality_JoinsNormally()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal(localityId: DefaultLocalityId);
        var matchingCluster = MakeCluster(lastSeenAgoHours: 0.1);
        matchingCluster.LocalityId = DefaultLocalityId;

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { matchingCluster });

        await svc.RouteSignalAsync(signal);

        await repo.Received(1).AttachToClusterAsync(
            matchingCluster.Id, signal.Id, signal.DeviceId, "join", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteSignal_WhenCandidateHasNullLocality_JoinsNormally()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal(localityId: DefaultLocalityId);
        var nullLocalityCluster = MakeCluster(lastSeenAgoHours: 0.1);
        nullLocalityCluster.LocalityId = null;

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { nullLocalityCluster });

        await svc.RouteSignalAsync(signal);

        // Null locality on cluster should not block join (pre-A2 clusters)
        await repo.Received(1).AttachToClusterAsync(
            nullLocalityCluster.Id, signal.Id, signal.DeviceId, "join", Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Phase A3: ClusterState wire format (snake_case)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_JoinToClusterInPossibleRestoration_ReturnsSnakeCaseState()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        var cluster = MakeCluster(lastSeenAgoHours: 0.1);
        cluster.State = SignalState.PossibleRestoration;

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { cluster });

        var result = await svc.RouteSignalAsync(signal);

        Assert.Equal("possible_restoration", result.ClusterState);
    }

    // -----------------------------------------------------------------------
    // B9: Location label propagation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RouteSignal_WhenNewCluster_CopiesLocationLabelFromSignal()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        signal.LocationLabelText = "Ngong Road near Adams Arcade, Kilimani";

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster>());
        SignalCluster? created = null;
        repo.CreateClusterAsync(Arg.Do<SignalCluster>(c => created = c), signal.Id, signal.DeviceId, Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<SignalCluster>()));

        await svc.RouteSignalAsync(signal);

        Assert.NotNull(created);
        Assert.Equal("Ngong Road near Adams Arcade, Kilimani", created!.LocationLabelText);
    }

    [Fact]
    public async Task RouteSignal_WhenJoinClusterWithoutLabel_BackfillsLocationLabel()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        signal.LocationLabelText = "Waiyaki Way, Westlands";

        var existingCluster = MakeCluster(lastSeenAgoHours: 0.1);
        existingCluster.LocationLabelText = null; // pre-B9 cluster without label

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        await svc.RouteSignalAsync(signal);

        Assert.Equal("Waiyaki Way, Westlands", existingCluster.LocationLabelText);
    }

    [Fact]
    public async Task RouteSignal_WhenJoinClusterWithExistingLabel_DoesNotOverwrite()
    {
        var (svc, repo, h3, civis) = Build();
        var signal = MakeSignal();
        signal.LocationLabelText = "Some other road";

        var existingCluster = MakeCluster(lastSeenAgoHours: 0.1);
        existingCluster.LocationLabelText = "Original label";

        h3.GetKRingCells(signal.SpatialCellId!, 1).Returns(new[] { signal.SpatialCellId! });
        repo.FindCandidateClustersAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CivicCategory>(), Arg.Any<CancellationToken>())
            .Returns(new List<SignalCluster> { existingCluster });

        await svc.RouteSignalAsync(signal);

        // Must preserve the original label, not overwrite
        Assert.Equal("Original label", existingCluster.LocationLabelText);
    }
}

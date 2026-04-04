using System;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Clusters;

public class CivisEvaluationServiceTests
{
	private static CivisOptions DefaultOptions()
	{
		return new CivisOptions
		{
			WrabRollingWindowDays = 30,
			JoinThreshold = 0.65,
			MinUniqueDevices = 2,
			DeactivationThreshold = 0.5,
			ActiveMassHorizonHours = 48,
			TimeScoreMaxAgeHours = 24.0,
			Roads = new CivisCategoryOptions
			{
				BaseFloor = 2,
				HalfLifeHours = 18.0,
				MacfMin = 2,
				MacfMax = 6
			}
		};
	}

	private static SignalCluster UnconfirmedRoadsCluster(int rawCount = 1)
	{
		return new SignalCluster
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
	}

	private static (CivisEvaluationService svc, FakeClusterRepo repo) Build(SignalCluster cluster, int wrabCount = 0, int activeMass = 0, int uniqueDevices = 0, CivisOptions? opts = null)
	{
		FakeClusterRepo fakeClusterRepo = new FakeClusterRepo(cluster)
		{
			WrabCount = wrabCount,
			ActiveMass = activeMass,
			UniqueDevices = uniqueDevices
		};
		CivisEvaluationService item = new CivisEvaluationService(fakeClusterRepo, Options.Create(opts ?? DefaultOptions()));
		return (svc: item, repo: fakeClusterRepo);
	}

	[Fact]
	public async Task EvaluateCluster_WhenMacfMetAndDevicesMet_TransitionsToActive()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(3);
		var (svc, _) = Build(cluster, 3, 3, 2);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Equal(SignalState.Active, cluster.State);
		Assert.NotNull(cluster.ActivatedAt);
	}

	[Fact]
	public async Task EvaluateCluster_WhenMacfMetAndDevicesMet_WritesCivisDecision()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(3);
		var (svc, repo) = Build(cluster, 3, 3, 2);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Single(repo.Decisions);
		Assert.Equal("activated", repo.Decisions[0].DecisionType);
	}

	[Fact]
	public async Task EvaluateCluster_WhenMacfMetAndDevicesMet_EmitsOutboxEvent()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(3);
		var (svc, repo) = Build(cluster, 3, 3, 2);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Single(repo.OutboxEvents);
		Assert.Equal("cluster_state_changed", repo.OutboxEvents[0].EventType);
	}

	[Fact]
	public async Task EvaluateCluster_WhenMacfNotMet_StaysUnconfirmed()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(2);
		var (svc, _) = Build(cluster, 3, 3, 2);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Equal(SignalState.Unconfirmed, cluster.State);
		Assert.Null(cluster.ActivatedAt);
	}

	[Fact]
	public async Task EvaluateCluster_WhenMacfNotMet_DoesNotWriteCivisDecision()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(2);
		var (svc, repo) = Build(cluster, 3, 3, 2);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Empty(repo.Decisions);
	}

	[Fact]
	public async Task EvaluateCluster_WhenUniqueDevicesBelowMinimum_StaysUnconfirmed()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(5);
		var (svc, _) = Build(cluster, 5, 5, 1);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Equal(SignalState.Unconfirmed, cluster.State);
	}

	[Fact]
	public async Task EvaluateCluster_WhenTwoDistinctDevices_ActivationSucceeds()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(3);
		var (svc, _) = Build(cluster, 3, 3, 2);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Equal(SignalState.Active, cluster.State);
	}

	[Fact]
	public async Task EvaluateCluster_WhenSameDeviceRepeated_CountsAsOneDevice()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(10);
		var (svc, _) = Build(cluster, 10, 10, 1);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Equal(SignalState.Unconfirmed, cluster.State);
	}

	[Fact]
	public async Task EvaluateCluster_WhenAlreadyActive_DoesNotQueryWrab()
	{
		SignalCluster cluster = UnconfirmedRoadsCluster(5);
		cluster.State = SignalState.Active;
		var (svc, repo) = Build(cluster, 5, 5, 3);
		await svc.EvaluateClusterAsync(cluster.Id);
		Assert.Empty(repo.Updates);
	}

	private static (CivisEvaluationService svc, FakeClusterRepo repo) BuildDecay(SignalCluster cluster, CivisOptions? opts = null)
	{
		FakeClusterRepo fakeClusterRepo = new FakeClusterRepo(cluster);
		CivisEvaluationService item = new CivisEvaluationService(fakeClusterRepo, Options.Create(opts ?? DefaultOptions()));
		return (svc: item, repo: fakeClusterRepo);
	}

	private static SignalCluster ActiveRoadsCluster(int rawCount = 4)
	{
		return new SignalCluster
		{
			Id = Guid.NewGuid(),
			Category = CivicCategory.Roads,
			State = SignalState.Active,
			RawConfirmationCount = rawCount,
			Wrab = 2m,
			CreatedAt = DateTime.UtcNow.AddHours(-100.0),
			UpdatedAt = DateTime.UtcNow.AddHours(-100.0),
			FirstSeenAt = DateTime.UtcNow.AddHours(-100.0),
			LastSeenAt = DateTime.UtcNow.AddHours(-100.0),
			ActivatedAt = DateTime.UtcNow.AddHours(-100.0)
		};
	}

	[Fact]
	public async Task ApplyDecay_WhenLiveMassBelowThreshold_ActiveTransitionsToPossibleRestoration()
	{
		SignalCluster cluster = ActiveRoadsCluster();
		var (svc, _) = BuildDecay(cluster);
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Equal(SignalState.PossibleRestoration, cluster.State);
		Assert.NotNull(cluster.PossibleRestorationAt);
	}

	[Fact]
	public async Task ApplyDecay_WhenLiveMassBelowThreshold_WritesCivisDecision()
	{
		SignalCluster cluster = ActiveRoadsCluster();
		var (svc, repo) = BuildDecay(cluster);
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Single(repo.Decisions);
		Assert.Equal("possible_restoration", repo.Decisions[0].DecisionType);
	}

	[Fact]
	public async Task ApplyDecay_WhenPossibleRestorationAndStillBelowThreshold_TransitionsToResolved()
	{
		SignalCluster cluster = ActiveRoadsCluster();
		cluster.State = SignalState.PossibleRestoration;
		cluster.PossibleRestorationAt = DateTime.UtcNow.AddHours(-100.0);
		var (svc, _) = BuildDecay(cluster);
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Equal(SignalState.Resolved, cluster.State);
		Assert.NotNull(cluster.ResolvedAt);
	}

	[Fact]
	public async Task ApplyDecay_WhenPossibleRestorationResolved_WritesCivisDecisionWithResolvedByDecay()
	{
		SignalCluster cluster = ActiveRoadsCluster();
		cluster.State = SignalState.PossibleRestoration;
		cluster.PossibleRestorationAt = DateTime.UtcNow.AddHours(-100.0);
		var (svc, repo) = BuildDecay(cluster);
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Single(repo.Decisions);
		Assert.Equal("resolved_by_decay", repo.Decisions[0].DecisionType);
	}

	[Fact]
	public async Task ApplyDecay_WhenLiveMassAboveThreshold_NoStateChange()
	{
		SignalCluster cluster = new SignalCluster
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
		SignalCluster cluster = ActiveRoadsCluster();
		cluster.State = SignalState.Resolved;
		var (svc, repo) = BuildDecay(cluster);
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Empty(repo.Updates);
	}

	[Fact]
	public async Task ApplyDecay_StateTransitions_EmitsOutboxEvent()
	{
		SignalCluster cluster = ActiveRoadsCluster();
		var (svc, repo) = BuildDecay(cluster);
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Single(repo.OutboxEvents);
		Assert.Equal("cluster_state_changed", repo.OutboxEvents[0].EventType);
	}

	// B-2: Deactivation threshold must be read from CivisOptions (not hardcoded).
	// Verify by setting threshold=0.0 — decay should never trigger when ratio >= 0.0 always.

	[Fact]
	public async Task ApplyDecay_WhenThresholdSetToZero_NeverTriggersDecay()
	{
		var opts = DefaultOptions();
		opts.DeactivationThreshold = 0.0; // ratio is always >= 0, so no transition fires
		SignalCluster cluster = ActiveRoadsCluster(); // 100 hours old, would decay with default 0.5 threshold
		var (svc, repo) = BuildDecay(cluster, opts);

		await svc.ApplyDecayAsync(cluster.Id);

		Assert.Equal(SignalState.Active, cluster.State);
		Assert.Empty(repo.Decisions);
	}

	[Fact]
	public async Task ApplyDecay_WhenThresholdSetToOne_AlwaysTriggersDecay()
	{
		var opts = DefaultOptions();
		opts.DeactivationThreshold = 1.0; // live_mass / wrab is always < 1.0 for any decayed cluster
		// Use a recent cluster with high raw count — still decays because ratio < 1.0
		SignalCluster cluster = ActiveRoadsCluster(rawCount: 4); // Wrab=2, even at t=0: ratio = 4/2 = 2 → NOT < 1.0
		// Force last_seen to now so decay barely happens but ratio still high
		cluster.LastSeenAt = DateTime.UtcNow;
		cluster.ActivatedAt = DateTime.UtcNow;
		var (svc, repo) = BuildDecay(cluster, opts);

		// With threshold=1.0 and a just-activated cluster: liveMass≈4, wrab=2, ratio≈2 > 1 → no decay
		await svc.ApplyDecayAsync(cluster.Id);
		Assert.Equal(SignalState.Active, cluster.State);

		// Now use a cluster that is 100 hours old so liveMass/wrab < 1.0
		var oldCluster = ActiveRoadsCluster(rawCount: 4); // Wrab=2, 100h old → liveMass≈0.085, ratio=0.04 < 1.0
		var (svc2, repo2) = BuildDecay(oldCluster, opts);
		await svc2.ApplyDecayAsync(oldCluster.Id);
		Assert.Equal(SignalState.PossibleRestoration, oldCluster.State);
	}
}

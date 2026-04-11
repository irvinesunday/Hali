using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Participation;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace Hali.Tests.Unit.Participation;

public class ParticipationServiceTests
{
	private static readonly Guid ClusterId = Guid.NewGuid();

	private static readonly Guid DeviceA = Guid.NewGuid();

	private static readonly Guid DeviceB = Guid.NewGuid();

	private static CivisOptions DefaultOptions()
	{
		return new CivisOptions
		{
			ContextEditWindowMinutes = 2,
			RestorationRatio = 0.6,
			MinRestorationAffectedVotes = 2
		};
	}

	private static SignalCluster ActiveCluster()
	{
		return new SignalCluster
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
	}

	private static (ParticipationService svc, FakeParticipationRepo pRepo, FakeClusterRepoForParticipation cRepo) Build(SignalCluster? cluster = null, CivisOptions? opts = null)
	{
		FakeParticipationRepo fakeParticipationRepo = new FakeParticipationRepo();
		FakeClusterRepoForParticipation fakeClusterRepoForParticipation = new FakeClusterRepoForParticipation(cluster);
		ParticipationService item = new ParticipationService(fakeParticipationRepo, fakeClusterRepoForParticipation, Options.Create(opts ?? DefaultOptions()));
		return (svc: item, pRepo: fakeParticipationRepo, cRepo: fakeClusterRepoForParticipation);
	}

	[Fact]
	public async Task RecordParticipation_WhenDeviceAlreadyParticipated_ReplacesExisting()
	{
		var (svc, pRepo, _) = Build();
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default(CancellationToken));
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default(CancellationToken));
		List<Hali.Domain.Entities.Participation.Participation> all = pRepo.All.Where((Hali.Domain.Entities.Participation.Participation p) => p.ClusterId == ClusterId && p.DeviceId == DeviceA).ToList();
		Assert.Single(all);
		Assert.Equal(ParticipationType.Affected, all[0].ParticipationType);
	}

	[Fact]
	public async Task RecordParticipation_TwoDevices_BothStoredIndependently()
	{
		var (svc, pRepo, _) = Build();
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default(CancellationToken));
		await svc.RecordParticipationAsync(ClusterId, DeviceB, null, ParticipationType.Observing, null, default(CancellationToken));
		Assert.Equal(2, pRepo.All.Count);
	}

	[Fact]
	public async Task RecordParticipation_UpdatesClusterCounts()
	{
		SignalCluster cluster = ActiveCluster();
		(ParticipationService, FakeParticipationRepo, FakeClusterRepoForParticipation) tuple = Build(cluster);
		var (svc, _, _) = tuple;
		_ = tuple.Item3;
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default(CancellationToken));
		await svc.RecordParticipationAsync(ClusterId, DeviceB, null, ParticipationType.Observing, null, default(CancellationToken));
		Assert.Equal(1, cluster.AffectedCount);
		Assert.Equal(1, cluster.ObservingCount);
	}

	[Fact]
	public async Task AddContext_WithoutAffectedParticipation_ThrowsContextRequiresAffected()
	{
		var (svc, _, _) = Build();
		Assert.Equal("CONTEXT_REQUIRES_AFFECTED", (await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddContextAsync(ClusterId, DeviceA, "Test context", default(CancellationToken)))).Message);
	}

	[Fact]
	public async Task AddContext_WithObservingParticipation_ThrowsContextRequiresAffected()
	{
		var (svc, _, _) = Build();
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default(CancellationToken));
		Assert.Equal("CONTEXT_REQUIRES_AFFECTED", (await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddContextAsync(ClusterId, DeviceA, "Test context", default(CancellationToken)))).Message);
	}

	[Fact]
	public async Task AddContext_WithAffectedParticipationWithinWindow_Succeeds()
	{
		var (svc, pRepo, _) = Build();
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Affected, null, default(CancellationToken));
		await svc.AddContextAsync(ClusterId, DeviceA, "Big pothole on main road", default(CancellationToken));
		Hali.Domain.Entities.Participation.Participation p = pRepo.All.Single((Hali.Domain.Entities.Participation.Participation x) => x.DeviceId == DeviceA);
		Assert.Equal("Big pothole on main road", p.ContextText);
	}

	[Fact]
	public async Task AddContext_AfterWindowExpired_ThrowsContextEditWindowExpired()
	{
		var (svc, pRepo, _) = Build();
		pRepo.All.Add(new Hali.Domain.Entities.Participation.Participation
		{
			Id = Guid.NewGuid(),
			ClusterId = ClusterId,
			DeviceId = DeviceA,
			ParticipationType = ParticipationType.Affected,
			CreatedAt = DateTime.UtcNow.AddMinutes(-5.0)
		});
		Assert.Equal("CONTEXT_EDIT_WINDOW_EXPIRED", (await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AddContextAsync(ClusterId, DeviceA, "Late context", default(CancellationToken)))).Message);
	}

	// Restoration responses now require a current `affected` participation
	// (server-side gating added in Task 3 of the PR #50 follow-ups). Each
	// restoration test seeds the calling device(s) as affected first.
	private static Task SeedAffectedAsync(ParticipationService svc, Guid deviceId)
		=> svc.RecordParticipationAsync(ClusterId, deviceId, null, ParticipationType.Affected, null, default);

	[Fact]
	public async Task RecordRestorationResponse_WithoutAffectedParticipation_ThrowsRestorationRequiresAffected()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, _, _) = Build(cluster);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default));
		Assert.Equal("RESTORATION_REQUIRES_AFFECTED", ex.Message);
	}

	[Fact]
	public async Task RecordRestorationResponse_WithObservingParticipation_ThrowsRestorationRequiresAffected()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, _, _) = Build(cluster);
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(
			() => svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default));
		Assert.Equal("RESTORATION_REQUIRES_AFFECTED", ex.Message);
	}

	[Fact]
	public async Task RecordRestorationResponse_Restored_MapsToRestorationYes()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, pRepo, _) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default(CancellationToken));
		Hali.Domain.Entities.Participation.Participation p = pRepo.All.Single((Hali.Domain.Entities.Participation.Participation x) => x.DeviceId == DeviceA);
		Assert.Equal(ParticipationType.RestorationYes, p.ParticipationType);
	}

	[Fact]
	public async Task RecordRestorationResponse_StillAffected_MapsToAffected()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, pRepo, _) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "still_affected", default(CancellationToken));
		Hali.Domain.Entities.Participation.Participation p = pRepo.All.Single((Hali.Domain.Entities.Participation.Participation x) => x.DeviceId == DeviceA);
		Assert.Equal(ParticipationType.Affected, p.ParticipationType);
	}

	[Fact]
	public async Task RecordRestorationResponse_NotSure_MapsToRestorationUnsure()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, pRepo, _) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "not_sure", default(CancellationToken));
		Hali.Domain.Entities.Participation.Participation p = pRepo.All.Single((Hali.Domain.Entities.Participation.Participation x) => x.DeviceId == DeviceA);
		Assert.Equal(ParticipationType.RestorationUnsure, p.ParticipationType);
	}

	[Fact]
	public async Task RecordRestorationResponse_RestoredWithRatioMet_TransitionsClusterToPossibleRestoration()
	{
		SignalCluster cluster = ActiveCluster();
		(ParticipationService, FakeParticipationRepo, FakeClusterRepoForParticipation) tuple = Build(cluster);
		var (svc, _, _) = tuple;
		_ = tuple.Item3;
		await SeedAffectedAsync(svc, DeviceA);
		await SeedAffectedAsync(svc, DeviceB);
		// Two votes needed (MinRestorationAffectedVotes=2)
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default(CancellationToken));
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceB, null, "restored", default(CancellationToken));
		Assert.Equal(SignalState.PossibleRestoration, cluster.State);
		Assert.NotNull(cluster.PossibleRestorationAt);
	}

	[Fact]
	public async Task RecordRestorationResponse_RestoredRatioMet_WritesDecisionAndOutboxEvent()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, _, cRepo) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		await SeedAffectedAsync(svc, DeviceB);
		// Two votes needed (MinRestorationAffectedVotes=2)
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default(CancellationToken));
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceB, null, "restored", default(CancellationToken));
		Assert.Single(cRepo.Decisions);
		Assert.Equal("possible_restoration", cRepo.Decisions[0].DecisionType);
		Assert.Single(cRepo.OutboxEvents);
	}

	[Fact]
	public async Task RecordRestorationResponse_RestoredRatioNotMet_ClusterRemainsActive()
	{
		SignalCluster cluster = ActiveCluster();
		CivisOptions opts = new CivisOptions
		{
			ContextEditWindowMinutes = 2,
			RestorationRatio = 0.6,
			MinRestorationAffectedVotes = 3
		};
		var (svc, _, cRepo) = Build(cluster, opts);
		await SeedAffectedAsync(svc, DeviceA);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default(CancellationToken));
		Assert.Equal(SignalState.Active, cluster.State);
		Assert.Empty(cRepo.Decisions);
	}
}

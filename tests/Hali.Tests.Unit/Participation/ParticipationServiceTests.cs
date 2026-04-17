using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
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
		var ex = await Assert.ThrowsAsync<ConflictException>(() => svc.AddContextAsync(ClusterId, DeviceA, "Test context", default(CancellationToken)));
		Assert.Equal("participation.context_requires_affected", ex.Code);
	}

	[Fact]
	public async Task AddContext_WithObservingParticipation_ThrowsContextRequiresAffected()
	{
		var (svc, _, _) = Build();
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default(CancellationToken));
		var ex = await Assert.ThrowsAsync<ConflictException>(() => svc.AddContextAsync(ClusterId, DeviceA, "Test context", default(CancellationToken)));
		Assert.Equal("participation.context_requires_affected", ex.Code);
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
		var ex = await Assert.ThrowsAsync<ConflictException>(() => svc.AddContextAsync(ClusterId, DeviceA, "Late context", default(CancellationToken)));
		Assert.Equal("participation.context_window_expired", ex.Code);
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
		var ex = await Assert.ThrowsAsync<ConflictException>(
			() => svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default));
		Assert.Equal("participation.restoration_requires_affected", ex.Code);
	}

	[Fact]
	public async Task RecordRestorationResponse_WithObservingParticipation_ThrowsRestorationRequiresAffected()
	{
		SignalCluster cluster = ActiveCluster();
		var (svc, _, _) = Build(cluster);
		await svc.RecordParticipationAsync(ClusterId, DeviceA, null, ParticipationType.Observing, null, default);
		var ex = await Assert.ThrowsAsync<ConflictException>(
			() => svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default));
		Assert.Equal("participation.restoration_requires_affected", ex.Code);
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
	public async Task RecordRestorationResponse_StillAffected_MapsToRestorationNo()
	{
		// Issue #142: the write path used to record "still_affected" as
		// ParticipationType.Affected, which excluded it from restoration
		// totals (CountRestorationResponses only counted types 3/4/5).
		// Post-fix it must be persisted as RestorationNo so dissenting
		// votes are visible to the restoration ratio gate.
		SignalCluster cluster = ActiveCluster();
		var (svc, pRepo, _) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "still_affected", default(CancellationToken));
		Hali.Domain.Entities.Participation.Participation p = pRepo.All.Single((Hali.Domain.Entities.Participation.Participation x) => x.DeviceId == DeviceA);
		Assert.Equal(ParticipationType.RestorationNo, p.ParticipationType);
	}

	[Fact]
	public async Task RecordRestorationResponse_StillAffected_DropsAffectedRowAndDecrementsAffectedCount()
	{
		// Confirms the corrected mapping flows through RefreshCountsAsync:
		// the seeded Affected row is replaced by a RestorationNo row, so
		// the cluster's AffectedCount falls back to 0. This documents the
		// AffectedCount semantic shift called out in #142's acceptance
		// criteria — Affected counts no longer include silent
		// "still_affected" restoration dissents.
		SignalCluster cluster = ActiveCluster();
		var (svc, pRepo, _) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		Assert.Equal(1, cluster.AffectedCount);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "still_affected", default(CancellationToken));
		Assert.Equal(0, cluster.AffectedCount);
		Assert.DoesNotContain(pRepo.All, x => x.DeviceId == DeviceA && x.ParticipationType == ParticipationType.Affected);
	}

	[Fact]
	public async Task EvaluateRestoration_IncludesStillAffectedDissentInTotal()
	{
		// Regression for #142: when one device votes "restored" and another
		// votes "still_affected", the ratio must be 1/2 (not 1/1). Pre-fix
		// the still_affected row was Affected and excluded from the total,
		// flipping the cluster to possible_restoration on a fully dissented
		// vote.
		SignalCluster cluster = ActiveCluster();
		var (svc, _, cRepo) = Build(cluster);
		await SeedAffectedAsync(svc, DeviceA);
		await SeedAffectedAsync(svc, DeviceB);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceA, null, "restored", default);
		await svc.RecordRestorationResponseAsync(ClusterId, DeviceB, null, "still_affected", default);
		// 1 yes / 2 total = 0.5 < RestorationRatio (0.6) → cluster must
		// remain Active and no possible_restoration decision must be written.
		Assert.Equal(SignalState.Active, cluster.State);
		Assert.Empty(cRepo.Decisions);
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

	[Fact]
	public async Task GetRestorationCountSnapshot_ReturnsYesNoTotalConsistently()
	{
		// Direct contract test for the new repository surface: yes + no +
		// unsure must equal total, and yes must never exceed total. This
		// covers #143's atomic-snapshot invariant at the repo boundary so
		// any future implementation (real EF or fake) can be checked
		// against the same shape.
		var pRepo = new FakeParticipationRepo();
		var clusterId = ClusterId;

		Hali.Domain.Entities.Participation.Participation Make(ParticipationType t, Guid device) => new()
		{
			Id = Guid.NewGuid(),
			ClusterId = clusterId,
			DeviceId = device,
			ParticipationType = t,
			CreatedAt = DateTime.UtcNow
		};

		// 3 yes, 2 no, 1 unsure, plus 1 unrelated Affected row that must not
		// be included in the totals.
		await pRepo.AddAsync(Make(ParticipationType.RestorationYes, Guid.NewGuid()), default);
		await pRepo.AddAsync(Make(ParticipationType.RestorationYes, Guid.NewGuid()), default);
		await pRepo.AddAsync(Make(ParticipationType.RestorationYes, Guid.NewGuid()), default);
		await pRepo.AddAsync(Make(ParticipationType.RestorationNo, Guid.NewGuid()), default);
		await pRepo.AddAsync(Make(ParticipationType.RestorationNo, Guid.NewGuid()), default);
		await pRepo.AddAsync(Make(ParticipationType.RestorationUnsure, Guid.NewGuid()), default);
		await pRepo.AddAsync(Make(ParticipationType.Affected, Guid.NewGuid()), default);

		RestorationCountSnapshot snapshot = await pRepo.GetRestorationCountSnapshotAsync(clusterId, default);

		Assert.Equal(3, snapshot.YesVotes);
		Assert.Equal(2, snapshot.NoVotes);
		Assert.Equal(6, snapshot.TotalResponses);
		Assert.True(snapshot.YesVotes <= snapshot.TotalResponses);
	}

	[Fact]
	public async Task GetRestorationCountSnapshot_NoRestorationVotes_ReturnsZeros()
	{
		var pRepo = new FakeParticipationRepo();
		RestorationCountSnapshot snapshot = await pRepo.GetRestorationCountSnapshotAsync(ClusterId, default);
		Assert.Equal(0, snapshot.YesVotes);
		Assert.Equal(0, snapshot.NoVotes);
		Assert.Equal(0, snapshot.TotalResponses);
	}
}

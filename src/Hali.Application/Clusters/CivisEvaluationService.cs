using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hali.Application.Clusters;

public class CivisEvaluationService : ICivisEvaluationService
{
	private readonly IClusterRepository _repo;

	private readonly CivisOptions _options;

	public CivisEvaluationService(IClusterRepository repo, IOptions<CivisOptions> options)
	{
		_repo = repo;
		_options = options.Value;
	}

	public async Task EvaluateClusterAsync(Guid clusterId, CancellationToken ct = default(CancellationToken))
	{
		SignalCluster cluster = await _repo.GetClusterByIdAsync(clusterId, ct);
		if (cluster != null && cluster.State == SignalState.Unconfirmed)
		{
			CivisCategoryOptions opts = _options.GetCategoryOptions(cluster.Category);
			int wrabCount = await _repo.ComputeWrabCountAsync(clusterId, _options.WrabRollingWindowDays, ct);
			int effectiveWrab = Math.Max(wrabCount, opts.BaseFloor);
			double sds = CivisCalculator.ComputeSds(await _repo.ComputeActiveMassCountAsync(clusterId, _options.ActiveMassHorizonHours, ct), wrabCount, opts.BaseFloor);
			int macf = CivisCalculator.ComputeMacf(sds, opts);
			int uniqueDevices = await _repo.CountUniqueDevicesAsync(clusterId, ct);
			DateTime now = DateTime.UtcNow;
			cluster.Wrab = effectiveWrab;
			cluster.Sds = (decimal)sds;
			cluster.Macf = macf;
			cluster.UpdatedAt = now;
			if (cluster.RawConfirmationCount >= macf && uniqueDevices >= _options.MinUniqueDevices)
			{
				cluster.State = SignalState.Active;
				cluster.ActivatedAt = now;
				await _repo.UpdateClusterAsync(cluster, ct);
				await _repo.WriteCivisDecisionAsync(new CivisDecision
				{
					Id = Guid.NewGuid(),
					ClusterId = clusterId,
					DecisionType = "activated",
					ReasonCodes = JsonSerializer.Serialize(new string[2] { "macf_met", "device_diversity_met" }),
					Metrics = JsonSerializer.Serialize(new
					{
						wrab_count = wrabCount,
						effective_wrab = effectiveWrab,
						sds = sds,
						macf = macf,
						raw_confirmation_count = cluster.RawConfirmationCount,
						unique_devices = uniqueDevices
					}),
					CreatedAt = now
				}, ct);
				await _repo.WriteOutboxEventAsync(new OutboxEvent
				{
					Id = Guid.NewGuid(),
					AggregateType = "cluster",
					AggregateId = clusterId,
					EventType = "cluster_state_changed",
					Payload = JsonSerializer.Serialize(new
					{
						cluster_id = clusterId,
						from_state = "unconfirmed",
						to_state = "active"
					}),
					OccurredAt = now
				}, ct);
			}
			else
			{
				await _repo.UpdateClusterAsync(cluster, ct);
			}
		}
	}

	public async Task ApplyDecayAsync(Guid clusterId, CancellationToken ct = default(CancellationToken))
	{
		SignalCluster cluster = await _repo.GetClusterByIdAsync(clusterId, ct);
		if (cluster == null || (cluster.State != SignalState.Active && cluster.State != SignalState.PossibleRestoration))
		{
			return;
		}
		CivisCategoryOptions opts = _options.GetCategoryOptions(cluster.Category);
		double lambda = CivisCalculator.ComputeLambda(opts.HalfLifeHours);
		double elapsedHours = (DateTime.UtcNow - cluster.LastSeenAt).TotalHours;
		double liveMass = CivisCalculator.ApplyDecay(cluster.RawConfirmationCount, lambda, elapsedHours);
		double effectiveWrab = (double)(cluster.Wrab ?? ((decimal)opts.BaseFloor));
		if (liveMass / effectiveWrab < _options.DeactivationThreshold)
		{
			DateTime now = DateTime.UtcNow;
			SignalState fromState = cluster.State;
			SignalState toState;
			if (cluster.State == SignalState.Active)
			{
				toState = SignalState.PossibleRestoration;
				cluster.State = SignalState.PossibleRestoration;
				cluster.PossibleRestorationAt = now;
			}
			else
			{
				toState = SignalState.Resolved;
				cluster.State = SignalState.Resolved;
				cluster.ResolvedAt = now;
			}
			cluster.UpdatedAt = now;
			await _repo.UpdateClusterAsync(cluster, ct);
			await _repo.WriteCivisDecisionAsync(new CivisDecision
			{
				Id = Guid.NewGuid(),
				ClusterId = clusterId,
				DecisionType = ((toState == SignalState.PossibleRestoration) ? "possible_restoration" : "resolved_by_decay"),
				ReasonCodes = JsonSerializer.Serialize(new string[1] { "decay_below_threshold" }),
				Metrics = JsonSerializer.Serialize(new
				{
					live_mass = liveMass,
					effective_wrab = effectiveWrab,
					elapsed_hours = elapsedHours,
					deactivation_threshold = _options.DeactivationThreshold
				}),
				CreatedAt = now
			}, ct);
			await _repo.WriteOutboxEventAsync(new OutboxEvent
			{
				Id = Guid.NewGuid(),
				AggregateType = "cluster",
				AggregateId = clusterId,
				EventType = "cluster_state_changed",
				Payload = JsonSerializer.Serialize(new
				{
					cluster_id = clusterId,
					from_state = fromState.ToString().ToLowerInvariant(),
					to_state = toState.ToString().ToLowerInvariant()
				}),
				OccurredAt = now
			}, ct);
		}
	}
}

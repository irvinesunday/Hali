using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hali.Application.Participation;

public class ParticipationService : IParticipationService
{
	private readonly IParticipationRepository _participationRepo;

	private readonly IClusterRepository _clusterRepo;

	private readonly CivisOptions _options;

	public ParticipationService(IParticipationRepository participationRepo, IClusterRepository clusterRepo, IOptions<CivisOptions> options)
	{
		_participationRepo = participationRepo;
		_clusterRepo = clusterRepo;
		_options = options.Value;
	}

	public async Task RecordParticipationAsync(Guid clusterId, Guid deviceId, Guid? accountId, ParticipationType type, string? idempotencyKey, CancellationToken ct)
	{
		await _participationRepo.DeleteByDeviceAsync(clusterId, deviceId, ct);
		await _participationRepo.AddAsync(new Hali.Domain.Entities.Participation.Participation
		{
			Id = Guid.NewGuid(),
			ClusterId = clusterId,
			DeviceId = deviceId,
			AccountId = accountId,
			ParticipationType = type,
			CreatedAt = DateTime.UtcNow,
			IdempotencyKey = idempotencyKey
		}, ct);
		await RefreshCountsAsync(clusterId, ct);
	}

	public async Task AddContextAsync(Guid clusterId, Guid deviceId, string contextText, CancellationToken ct)
	{
		Hali.Domain.Entities.Participation.Participation participation = await _participationRepo.GetByDeviceAsync(clusterId, deviceId, ct);
		if (participation == null || participation.ParticipationType != ParticipationType.Affected)
		{
			throw new InvalidOperationException("CONTEXT_REQUIRES_AFFECTED");
		}
		DateTime windowExpiry = participation.CreatedAt.AddMinutes(_options.ContextEditWindowMinutes);
		if (DateTime.UtcNow > windowExpiry)
		{
			throw new InvalidOperationException("CONTEXT_EDIT_WINDOW_EXPIRED");
		}
		await _participationRepo.UpdateContextAsync(participation.Id, contextText, ct);
	}

	public async Task RecordRestorationResponseAsync(Guid clusterId, Guid deviceId, Guid? accountId, string response, CancellationToken ct)
	{
		if (1 == 0)
		{
		}
		ParticipationType participationType = response switch
		{
			"restored" => ParticipationType.RestorationYes, 
			"still_affected" => ParticipationType.Affected, 
			"not_sure" => ParticipationType.RestorationUnsure, 
			_ => throw new InvalidOperationException("RESTORATION_INVALID_RESPONSE"), 
		};
		if (1 == 0)
		{
		}
		ParticipationType type = participationType;
		await RecordParticipationAsync(clusterId, deviceId, accountId, type, null, ct);
		await EvaluateRestorationAsync(clusterId, ct);
	}

	private async Task EvaluateRestorationAsync(Guid clusterId, CancellationToken ct)
	{
		SignalCluster cluster = await _clusterRepo.GetClusterByIdAsync(clusterId, ct);
		if (cluster == null || cluster.State != SignalState.Active)
		{
			return;
		}
		int restorationYes = await _participationRepo.CountByTypeAsync(clusterId, ParticipationType.RestorationYes, ct);
		int totalResponses = await _participationRepo.CountRestorationResponsesAsync(clusterId, ct);
		if (totalResponses >= _options.MinRestorationAffectedVotes)
		{
			double ratio = (double)restorationYes / (double)totalResponses;
			if (!(ratio < _options.RestorationRatio))
			{
				DateTime now = DateTime.UtcNow;
				cluster.State = SignalState.PossibleRestoration;
				cluster.PossibleRestorationAt = now;
				cluster.UpdatedAt = now;
				await _clusterRepo.UpdateClusterAsync(cluster, ct);
				await _clusterRepo.WriteCivisDecisionAsync(new CivisDecision
				{
					Id = Guid.NewGuid(),
					ClusterId = clusterId,
					DecisionType = "possible_restoration",
					ReasonCodes = JsonSerializer.Serialize(new string[1] { "restoration_ratio_met" }),
					Metrics = JsonSerializer.Serialize(new
					{
						restoration_yes = restorationYes,
						total_responses = totalResponses,
						ratio = ratio,
						threshold = _options.RestorationRatio
					}),
					CreatedAt = now
				}, ct);
				await _clusterRepo.WriteOutboxEventAsync(new OutboxEvent
				{
					Id = Guid.NewGuid(),
					AggregateType = "cluster",
					AggregateId = clusterId,
					EventType = "cluster_state_changed",
					Payload = JsonSerializer.Serialize(new
					{
						cluster_id = clusterId,
						from_state = "active",
						to_state = "possible_restoration"
					}),
					OccurredAt = now
				}, ct);
			}
		}
	}

	private async Task RefreshCountsAsync(Guid clusterId, CancellationToken ct)
	{
		int affected = await _participationRepo.CountByTypeAsync(clusterId, ParticipationType.Affected, ct);
		int observing = await _participationRepo.CountByTypeAsync(clusterId, ParticipationType.Observing, ct);
		await _clusterRepo.UpdateCountsAsync(clusterId, affected, observing, ct);
	}
}

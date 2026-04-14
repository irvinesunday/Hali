using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Participation;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Application.Participation;

public class ParticipationService : IParticipationService
{
    private readonly IParticipationRepository _participationRepo;

    private readonly IClusterRepository _clusterRepo;

    private readonly CivisOptions _options;

    private readonly ILogger<ParticipationService>? _logger;

    public ParticipationService(IParticipationRepository participationRepo, IClusterRepository clusterRepo, IOptions<CivisOptions> options, ILogger<ParticipationService>? logger = null)
    {
        _participationRepo = participationRepo;
        _clusterRepo = clusterRepo;
        _options = options.Value;
        _logger = logger;
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
        Hali.Domain.Entities.Participation.Participation? participation = await _participationRepo.GetByDeviceAsync(clusterId, deviceId, ct);
        if (participation == null || participation.ParticipationType != ParticipationType.Affected)
        {
            throw new ConflictException("participation.context_requires_affected", "Context requires an active affected participation.");
        }
        DateTime windowExpiry = participation.CreatedAt.AddMinutes(_options.ContextEditWindowMinutes);
        if (DateTime.UtcNow > windowExpiry)
        {
            throw new ConflictException("participation.context_window_expired", "Context edit window has expired.");
        }
        await _participationRepo.UpdateContextAsync(participation.Id, contextText, ct);
    }

    public async Task RecordRestorationResponseAsync(Guid clusterId, Guid deviceId, Guid? accountId, string response, CancellationToken ct)
    {
        // Server-side gating: the caller must currently hold an `affected`
        // participation on this cluster. The mobile app gates the CTA on
        // myParticipation.canRespondToRestoration; this re-check enforces
        // the rule at the trust boundary, not just the UI.
        var current = await _participationRepo.GetByDeviceAsync(clusterId, deviceId, ct);
        if (current == null && accountId.HasValue)
        {
            current = await _participationRepo.GetMostRecentByAccountAsync(clusterId, accountId.Value, ct);
        }
        if (current == null || current.ParticipationType != ParticipationType.Affected)
        {
            throw new ConflictException("participation.restoration_requires_affected", "Restoration response requires an active affected participation.");
        }

        ParticipationType participationType = response switch
        {
            "restored" => ParticipationType.RestorationYes,
            "still_affected" => ParticipationType.RestorationNo,
            "not_sure" => ParticipationType.RestorationUnsure,
            _ => throw new ValidationException("Invalid restoration response value.", code: "validation.invalid_restoration_response"),
        };
        await RecordParticipationAsync(clusterId, deviceId, accountId, participationType, null, ct);
        await EvaluateRestorationAsync(clusterId, ct);
    }

    private async Task EvaluateRestorationAsync(Guid clusterId, CancellationToken ct)
    {
        SignalCluster cluster = await _clusterRepo.GetClusterByIdAsync(clusterId, ct);
        if (cluster == null || cluster.State != SignalState.Active)
        {
            return;
        }
        // Atomic single-query snapshot — see issue #143. Two-query pattern
        // (CountByType(RestorationYes) + CountRestorationResponses) could see
        // an inconsistent yes/total under concurrent participation-type flips.
        RestorationCountSnapshot snapshot = await _participationRepo.GetRestorationCountSnapshotAsync(clusterId, ct);
        int restorationYes = snapshot.YesVotes;
        int totalResponses = snapshot.TotalResponses;
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

                _logger?.LogInformation(
                    "{EventName} clusterId={ClusterId} restorationYes={RestorationYes} totalResponses={TotalResponses}",
                    ObservabilityEvents.ClusterPossibleRestoration, clusterId, restorationYes, totalResponses);
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

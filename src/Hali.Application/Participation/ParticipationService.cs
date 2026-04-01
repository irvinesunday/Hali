using System.Text.Json;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using ParticipationEntity = Hali.Domain.Entities.Participation.Participation;

namespace Hali.Application.Participation;

public class ParticipationService : IParticipationService
{
    private readonly IParticipationRepository _participationRepo;
    private readonly IClusterRepository _clusterRepo;
    private readonly CivisOptions _options;

    public ParticipationService(
        IParticipationRepository participationRepo,
        IClusterRepository clusterRepo,
        IOptions<CivisOptions> options)
    {
        _participationRepo = participationRepo;
        _clusterRepo = clusterRepo;
        _options = options.Value;
    }

    public async Task RecordParticipationAsync(
        Guid clusterId,
        Guid deviceId,
        Guid? accountId,
        ParticipationType type,
        string? idempotencyKey,
        CancellationToken ct)
    {
        // One participation type per device per cluster — replace any existing
        await _participationRepo.DeleteByDeviceAsync(clusterId, deviceId, ct);

        await _participationRepo.AddAsync(new ParticipationEntity
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

    public async Task AddContextAsync(
        Guid clusterId,
        Guid deviceId,
        string contextText,
        CancellationToken ct)
    {
        var participation = await _participationRepo.GetByDeviceAsync(clusterId, deviceId, ct);

        if (participation is null || participation.ParticipationType != ParticipationType.Affected)
            throw new InvalidOperationException("CONTEXT_REQUIRES_AFFECTED");

        var windowExpiry = participation.CreatedAt.AddMinutes(_options.ContextEditWindowMinutes);
        if (DateTime.UtcNow > windowExpiry)
            throw new InvalidOperationException("CONTEXT_EDIT_WINDOW_EXPIRED");

        await _participationRepo.UpdateContextAsync(participation.Id, contextText, ct);
    }

    public async Task RecordRestorationResponseAsync(
        Guid clusterId,
        Guid deviceId,
        Guid? accountId,
        string response,
        CancellationToken ct)
    {
        var type = response switch
        {
            "restored" => ParticipationType.RestorationYes,
            "still_affected" => ParticipationType.Affected,
            "not_sure" => ParticipationType.RestorationUnsure,
            _ => throw new InvalidOperationException("RESTORATION_INVALID_RESPONSE")
        };

        await RecordParticipationAsync(clusterId, deviceId, accountId, type, idempotencyKey: null, ct);
        await EvaluateRestorationAsync(clusterId, ct);
    }

    private async Task EvaluateRestorationAsync(Guid clusterId, CancellationToken ct)
    {
        var cluster = await _clusterRepo.GetClusterByIdAsync(clusterId, ct);
        if (cluster is null || cluster.State != SignalState.Active)
            return;

        var restorationYes = await _participationRepo.CountByTypeAsync(clusterId, ParticipationType.RestorationYes, ct);
        var totalResponses = await _participationRepo.CountRestorationResponsesAsync(clusterId, ct);

        if (totalResponses < _options.MinRestorationAffectedVotes)
            return;

        var ratio = (double)restorationYes / totalResponses;
        if (ratio < _options.RestorationRatio)
            return;

        var now = DateTime.UtcNow;
        cluster.State = SignalState.PossibleRestoration;
        cluster.PossibleRestorationAt = now;
        cluster.UpdatedAt = now;
        await _clusterRepo.UpdateClusterAsync(cluster, ct);

        await _clusterRepo.WriteCivisDecisionAsync(new CivisDecision
        {
            Id = Guid.NewGuid(),
            ClusterId = clusterId,
            DecisionType = "possible_restoration",
            ReasonCodes = JsonSerializer.Serialize(new[] { "restoration_ratio_met" }),
            Metrics = JsonSerializer.Serialize(new
            {
                restoration_yes = restorationYes,
                total_responses = totalResponses,
                ratio,
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

    private async Task RefreshCountsAsync(Guid clusterId, CancellationToken ct)
    {
        var affected = await _participationRepo.CountByTypeAsync(clusterId, ParticipationType.Affected, ct);
        var observing = await _participationRepo.CountByTypeAsync(clusterId, ParticipationType.Observing, ct);
        await _clusterRepo.UpdateCountsAsync(clusterId, affected, observing, ct);
    }
}

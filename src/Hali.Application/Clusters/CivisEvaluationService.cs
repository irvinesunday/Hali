using System.Text.Json;
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

    public async Task EvaluateClusterAsync(Guid clusterId, CancellationToken ct = default)
    {
        var cluster = await _repo.GetClusterByIdAsync(clusterId, ct);
        if (cluster is null || cluster.State != SignalState.Unconfirmed)
            return;

        var opts = _options.GetCategoryOptions(cluster.Category);

        // WRAB — count of linked events in rolling window; effective = max(WRAB, base_floor)
        var wrabCount = await _repo.ComputeWrabCountAsync(clusterId, _options.WrabRollingWindowDays, ct);
        var effectiveWrab = Math.Max(wrabCount, opts.BaseFloor);

        // Active mass — count in evaluation horizon
        var activeMass = await _repo.ComputeActiveMassCountAsync(clusterId, _options.ActiveMassHorizonHours, ct);

        // SDS and MACF
        var sds = CivisCalculator.ComputeSds(activeMass, wrabCount, opts.BaseFloor);
        var macf = CivisCalculator.ComputeMacf(sds, opts);

        // Unique device check
        var uniqueDevices = await _repo.CountUniqueDevicesAsync(clusterId, ct);

        var now = DateTime.UtcNow;

        // Update metrics on cluster
        cluster.Wrab = (decimal)effectiveWrab;
        cluster.Sds = (decimal)sds;
        cluster.Macf = macf;
        cluster.UpdatedAt = now;

        // Activation gate
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
                ReasonCodes = JsonSerializer.Serialize(new[] { "macf_met", "device_diversity_met" }),
                Metrics = JsonSerializer.Serialize(new
                {
                    wrab_count = wrabCount,
                    effective_wrab = effectiveWrab,
                    sds,
                    macf,
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

    public async Task ApplyDecayAsync(Guid clusterId, CancellationToken ct = default)
    {
        var cluster = await _repo.GetClusterByIdAsync(clusterId, ct);
        if (cluster is null
            || (cluster.State != SignalState.Active && cluster.State != SignalState.PossibleRestoration))
            return;

        var opts = _options.GetCategoryOptions(cluster.Category);
        var lambda = CivisCalculator.ComputeLambda(opts.HalfLifeHours);
        var elapsedHours = (DateTime.UtcNow - cluster.LastSeenAt).TotalHours;
        var liveMass = CivisCalculator.ApplyDecay(cluster.RawConfirmationCount, lambda, elapsedHours);

        var effectiveWrab = (double)(cluster.Wrab ?? opts.BaseFloor);
        var isBelow = liveMass / effectiveWrab < _options.DeactivationThreshold;

        if (!isBelow)
            return;

        var now = DateTime.UtcNow;
        var fromState = cluster.State;
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
            DecisionType = toState == SignalState.PossibleRestoration ? "possible_restoration" : "resolved_by_decay",
            ReasonCodes = JsonSerializer.Serialize(new[] { "decay_below_threshold" }),
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

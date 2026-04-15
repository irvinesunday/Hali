using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Application.Clusters;

public class CivisEvaluationService : ICivisEvaluationService
{
    private readonly IClusterRepository _repo;

    private readonly CivisOptions _options;

    private readonly INotificationQueueService? _notificationQueue;

    private readonly ILogger<CivisEvaluationService>? _logger;

    private readonly SignalsMetrics? _metrics;

    private readonly ClustersMetrics? _clustersMetrics;

    public CivisEvaluationService(IClusterRepository repo, IOptions<CivisOptions> options,
        INotificationQueueService? notificationQueue = null,
        ILogger<CivisEvaluationService>? logger = null,
        SignalsMetrics? metrics = null,
        ClustersMetrics? clustersMetrics = null)
    {
        _repo = repo;
        _options = options.Value;
        _notificationQueue = notificationQueue;
        _logger = logger;
        _metrics = metrics;
        _clustersMetrics = clustersMetrics;
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
            double minLocationConfidence = await _repo.GetMinLocationConfidenceAsync(clusterId, ct);
            int macf = CivisCalculator.ComputeMacf(
                sds,
                opts,
                cluster.Category == CivicCategory.Safety,
                minLocationConfidence);
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

                _logger?.LogInformation(
                    "{EventName} clusterId={ClusterId} localityId={LocalityId} category={Category}",
                    ObservabilityEvents.ClusterActivated, clusterId, cluster.LocalityId, cluster.Category);

                // Activation counter fires at the same decision point as the
                // structured ClusterActivated log — once per unconfirmed →
                // active transition. Operators can compute the activation
                // rate directly (no need to derive it from log joins), and
                // the bucket stays consistent with the join-outcome counter
                // emitted earlier in ClusteringService.
                _metrics?.SignalJoinOutcomeTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        SignalsMetrics.TagOutcome,
                        SignalsMetrics.JoinOutcomeActivatedCluster));

                // Lifecycle counter (R3.c / #168) — cluster-scoped companion
                // to the signal-scoped activated_cluster outcome above. Both
                // agree on cardinality for this transition (one increment
                // per unconfirmed→active flip) but the lifecycle counter is
                // the canonical source across all three state transitions
                // (active→possible_restoration and
                // possible_restoration→resolved are covered in ApplyDecay
                // and ParticipationService.EvaluateRestorationAsync).
                _clustersMetrics?.ClusterLifecycleTransitionsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        ClustersMetrics.TagFromState,
                        ClustersMetrics.StateUnconfirmed),
                    new KeyValuePair<string, object?>(
                        ClustersMetrics.TagToState,
                        ClustersMetrics.StateActive));

                if (_notificationQueue != null)
                {
                    try
                    {
                        await _notificationQueue.QueueClusterActivatedAsync(
                            clusterId, cluster.LocalityId,
                            cluster.Title ?? "New civic issue",
                            cluster.Summary ?? string.Empty,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to queue cluster_activated notifications for {ClusterId}", clusterId);
                    }
                }
            }
            else
            {
                await _repo.UpdateClusterAsync(cluster, ct);
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="SignalState"/> to the canonical snake_case tag
    /// value used by <see cref="ClustersMetrics.ClusterLifecycleTransitionsTotal"/>.
    /// Only the three lifecycle states that participate in transitions
    /// emitted by this service are mapped.
    /// </summary>
    private static string ToStateTag(SignalState state) => state switch
    {
        SignalState.Active => ClustersMetrics.StateActive,
        SignalState.PossibleRestoration => ClustersMetrics.StatePossibleRestoration,
        SignalState.Resolved => ClustersMetrics.StateResolved,
        SignalState.Unconfirmed => ClustersMetrics.StateUnconfirmed,
        _ => state.ToString().ToLowerInvariant(),
    };

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

            // Lifecycle counter (R3.c / #168) — fires at the same
            // transition point as the outbox event and the
            // ClusterPossibleRestoration / ClusterResolvedByDecay log
            // events below. Tag values come from the ClustersMetrics
            // state catalog (canonical snake_case) rather than the raw
            // enum ToString, because `SignalState.PossibleRestoration`
            // lowercases to "possiblerestoration" without an underscore —
            // using the catalog keeps metric tags uniform and alertable
            // (`to_state="possible_restoration"`).
            _clustersMetrics?.ClusterLifecycleTransitionsTotal.Add(
                1,
                new KeyValuePair<string, object?>(
                    ClustersMetrics.TagFromState,
                    ToStateTag(fromState)),
                new KeyValuePair<string, object?>(
                    ClustersMetrics.TagToState,
                    ToStateTag(toState)));

            if (_notificationQueue != null)
            {
                try
                {
                    if (toState == SignalState.PossibleRestoration)
                    {
                        _logger?.LogInformation("{EventName} clusterId={ClusterId}",
                            ObservabilityEvents.ClusterPossibleRestoration, clusterId);
                        await _notificationQueue.QueueRestorationPromptAsync(clusterId, cluster.Title ?? "Civic issue", ct);
                    }
                    else if (toState == SignalState.Resolved)
                    {
                        _logger?.LogInformation("{EventName} clusterId={ClusterId}",
                            ObservabilityEvents.ClusterResolvedByDecay, clusterId);
                        await _notificationQueue.QueueClusterResolvedAsync(clusterId, cluster.LocalityId, cluster.Title ?? "Civic issue", ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to queue decay notifications for {ClusterId}", clusterId);
                }
            }
        }
    }
}

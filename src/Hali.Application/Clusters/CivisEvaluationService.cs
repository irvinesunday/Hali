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

    private readonly ICorrelationContext? _correlationContext;

    public CivisEvaluationService(IClusterRepository repo, IOptions<CivisOptions> options,
        INotificationQueueService? notificationQueue = null,
        ILogger<CivisEvaluationService>? logger = null,
        SignalsMetrics? metrics = null,
        ClustersMetrics? clustersMetrics = null,
        ICorrelationContext? correlationContext = null)
    {
        _repo = repo;
        _options = options.Value;
        _notificationQueue = notificationQueue;
        _logger = logger;
        _metrics = metrics;
        _clustersMetrics = clustersMetrics;
        _correlationContext = correlationContext;
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
                CivisDecision activationDecision = new CivisDecision
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
                };
                OutboxEvent activationEvent = new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "signal_cluster",
                    AggregateId = clusterId,
                    EventType = ObservabilityEvents.ClusterActivated,
                    SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                    Payload = JsonSerializer.Serialize(new
                    {
                        cluster_id = clusterId,
                        from = ClustersMetrics.StateUnconfirmed,
                        to = ClustersMetrics.StateActive,
                        reason_code = "macf_met"
                    }),
                    OccurredAt = now,
                    CorrelationId = _correlationContext?.CurrentCorrelationId ?? Guid.NewGuid(),
                    CausationId = null,
                };
                await _repo.ApplyClusterTransitionAsync(cluster, activationDecision, activationEvent, ct);

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
    /// Maps a <see cref="SignalState"/> to its canonical snake_case wire
    /// value. Used for both
    /// <c>cluster_lifecycle_transitions_total</c> metric tags
    /// (<see cref="ClustersMetrics.ClusterLifecycleTransitionsTotal"/>)
    /// and the <c>cluster_state_changed</c> outbox payload's
    /// <c>from_state</c> / <c>to_state</c> fields, so metric dashboards and
    /// downstream outbox consumers observe the same string for the same
    /// transition.
    ///
    /// Explicit mapping is deliberate. Using <c>state.ToString().ToLowerInvariant()</c>
    /// collapses <c>PossibleRestoration</c> to <c>"possiblerestoration"</c>
    /// (no underscore), which breaks the canonical snake_case contract
    /// required by consumers matching on <c>to_state == "possible_restoration"</c>
    /// (see <c>docs/arch/CODING_STANDARDS.md</c> §Enum serialization rules).
    /// Only the four states that participate in transitions emitted by this
    /// service are mapped; any other value indicates a caller bug and throws.
    /// </summary>
    private static string ToCanonicalStateString(SignalState state) => state switch
    {
        SignalState.Unconfirmed => ClustersMetrics.StateUnconfirmed,
        SignalState.Active => ClustersMetrics.StateActive,
        SignalState.PossibleRestoration => ClustersMetrics.StatePossibleRestoration,
        SignalState.Resolved => ClustersMetrics.StateResolved,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state), state,
            "SignalState is not a lifecycle transition state emitted by CivisEvaluationService."),
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
            string decayEventType = toState == SignalState.PossibleRestoration
                ? ObservabilityEvents.ClusterPossibleRestoration
                : ObservabilityEvents.ClusterResolvedByDecay;
            CivisDecision decayDecision = new CivisDecision
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                DecisionType = toState == SignalState.PossibleRestoration ? "possible_restoration" : "resolved_by_decay",
                ReasonCodes = JsonSerializer.Serialize(new string[1] { "decay_below_threshold" }),
                Metrics = JsonSerializer.Serialize(new
                {
                    live_mass = liveMass,
                    effective_wrab = effectiveWrab,
                    elapsed_hours = elapsedHours,
                    deactivation_threshold = _options.DeactivationThreshold
                }),
                CreatedAt = now
            };
            // Decay runs from a periodic worker with no parent HTTP request.
            // Apply the worker correlation rule: if the context carries a
            // non-empty id (propagated from an outbox event), use it; otherwise
            // generate a new root so the worker's own job-span is traceable.
            var decayCorrelationId = _correlationContext is not null
                && _correlationContext.CurrentCorrelationId != Guid.Empty
                    ? _correlationContext.CurrentCorrelationId
                    : _correlationContext?.CreateNewCorrelationId() ?? Guid.NewGuid();
            OutboxEvent decayEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "signal_cluster",
                AggregateId = clusterId,
                EventType = decayEventType,
                SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = clusterId,
                    from = ToCanonicalStateString(fromState),
                    to = ToCanonicalStateString(toState),
                    trigger = "decay",
                    reason_code = "decay_below_threshold"
                }),
                OccurredAt = now,
                CorrelationId = decayCorrelationId,
                CausationId = null,
            };
            await _repo.ApplyClusterTransitionAsync(cluster, decayDecision, decayEvent, ct);

            // Lifecycle counter (R3.c / #168) — fires at the same
            // transition point as the outbox event and the
            // ClusterPossibleRestoration / ClusterResolvedByDecay log
            // events below. Tag values come from ToCanonicalStateString,
            // the same helper used by the outbox payload above, so metric
            // dashboards and downstream outbox consumers observe the same
            // string for the same transition.
            _clustersMetrics?.ClusterLifecycleTransitionsTotal.Add(
                1,
                new KeyValuePair<string, object?>(
                    ClustersMetrics.TagFromState,
                    ToCanonicalStateString(fromState)),
                new KeyValuePair<string, object?>(
                    ClustersMetrics.TagToState,
                    ToCanonicalStateString(toState)));

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

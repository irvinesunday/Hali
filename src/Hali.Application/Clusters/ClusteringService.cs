using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Application.Clusters;

public class ClusteringService : IClusteringService
{
    private readonly IClusterRepository _repo;

    private readonly IH3CellService _h3;

    private readonly ICivisEvaluationService _civis;

    private readonly CivisOptions _options;

    private readonly ILogger<ClusteringService>? _logger;

    private readonly SignalsMetrics? _metrics;

    public ClusteringService(
        IClusterRepository repo,
        IH3CellService h3,
        ICivisEvaluationService civis,
        IOptions<CivisOptions> options,
        ILogger<ClusteringService>? logger = null,
        SignalsMetrics? metrics = null)
    {
        _repo = repo;
        _h3 = h3;
        _civis = civis;
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<ClusterRoutingResult> RouteSignalAsync(SignalEvent signal, CancellationToken ct = default(CancellationToken))
    {
        if (signal.SpatialCellId is null)
        {
            // H3 retype (#153): was a bare InvalidOperationException deferred
            // from PR #134. The typed InvariantViolationException preserves
            // the structured `clustering.no_spatial_cell` code on logs/traces
            // (see ErrorCodes.InternalOnlyCodes) while
            // ExceptionToApiErrorMapper redacts ErrorCategory.Unexpected on
            // the wire to ServerInternalError. No public-wire change: the
            // response remains 500 with `server.internal_error`, identical
            // to the pre-H3 mapper-fallback behaviour.
            throw new InvariantViolationException(
                ErrorCodes.ClusteringNoSpatialCell,
                "Signal reached the clustering pipeline without a derived spatial cell.");
        }
        string[] searchCells = _h3.GetKRingCells(signal.SpatialCellId, 1);
        IReadOnlyList<SignalCluster> candidates = await _repo.FindCandidateClustersAsync(searchCells, signal.Category, ct);
        SignalCluster? bestCluster = null;
        double bestScore = 0.0;
        foreach (SignalCluster candidate in candidates)
        {
            // Skip candidates with a different locality to prevent inconsistency
            if (signal.LocalityId.HasValue
                && candidate.LocalityId.HasValue
                && candidate.LocalityId.Value != signal.LocalityId.Value)
            {
                continue;
            }

            double score = ComputeJoinScore(signal, candidate);
            if (score >= _options.JoinThreshold && score > bestScore)
            {
                bestScore = score;
                bestCluster = candidate;
            }
        }
        if (bestCluster is not null)
        {
            await _repo.AttachToClusterAsync(bestCluster.Id, signal.Id, signal.DeviceId, "join", ct);
            bestCluster.LastSeenAt = DateTime.UtcNow;
            bestCluster.UpdatedAt = DateTime.UtcNow;
            bestCluster.RawConfirmationCount++;
            if (!string.IsNullOrEmpty(signal.ConditionSlug))
            {
                bestCluster.DominantConditionSlug = signal.ConditionSlug;
            }
            // Backfill location label if the cluster was created before B9
            if (string.IsNullOrEmpty(bestCluster.LocationLabelText)
                && !string.IsNullOrEmpty(signal.LocationLabelText))
            {
                bestCluster.LocationLabelText = signal.LocationLabelText;
            }
            await _repo.UpdateClusterAsync(bestCluster, ct);
            // `cluster.updated` is a soft-update event (raw_confirmation_count bump on
            // join) — distinct from the lifecycle transitions in the canonical outbox
            // table (see docs/arch/02_api_contracts.md §Outbox event envelope). The
            // AggregateType + SchemaVersion fields follow the same envelope shape as
            // the lifecycle events so consumers can parse uniformly.
            await _repo.WriteOutboxEventAsync(new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "signal_cluster",
                AggregateId = bestCluster.Id,
                EventType = "cluster.updated",
                SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = bestCluster.Id,
                    signal_event_id = signal.Id,
                    raw_confirmation_count = bestCluster.RawConfirmationCount
                }),
                OccurredAt = DateTime.UtcNow
            }, ct);
            await _civis.EvaluateClusterAsync(bestCluster.Id, ct);

            _logger?.LogInformation(
                "{EventName} clusterId={ClusterId} outcome={Outcome} joinScore={JoinScore} candidateCount={CandidateCount}",
                ObservabilityEvents.SignalRouted, bestCluster.Id, "joined", bestScore, candidates.Count);

            // Join-outcome counter fires once here — at the true decision
            // point — so the metric reflects what actually happened rather
            // than inferring from downstream state. The activation bucket is
            // incremented separately in CivisEvaluationService if and only if
            // the submission flipped the cluster to Active.
            _metrics?.SignalJoinOutcomeTotal.Add(
                1,
                new KeyValuePair<string, object?>(
                    SignalsMetrics.TagOutcome,
                    SignalsMetrics.JoinOutcomeJoinedExisting));

            return new ClusterRoutingResult(
                bestCluster.Id,
                WasCreated: false,
                WasJoined: true,
                ToSnakeCase(bestCluster.State),
                bestCluster.LocalityId);
        }
        else
        {
            DateTime now = DateTime.UtcNow;
            SignalCluster newCluster = new SignalCluster
            {
                Id = Guid.NewGuid(),
                LocalityId = signal.LocalityId,
                Category = signal.Category,
                SubcategorySlug = signal.SubcategorySlug,
                DominantConditionSlug = signal.ConditionSlug,
                State = SignalState.Unconfirmed,
                SpatialCellId = signal.SpatialCellId,
                TemporalType = (signal.TemporalType ?? "episodic_unknown"),
                Title = BuildClusterTitle(signal),
                Summary = (signal.NeutralSummary ?? string.Empty),
                LocationLabelText = signal.LocationLabelText,
                CreatedAt = now,
                UpdatedAt = now,
                FirstSeenAt = now,
                LastSeenAt = now,
                RawConfirmationCount = 1
            };
            await _repo.CreateClusterAsync(newCluster, signal.Id, signal.DeviceId, ct);
            await _repo.WriteOutboxEventAsync(new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "signal_cluster",
                AggregateId = newCluster.Id,
                EventType = ObservabilityEvents.ClusterCreated,
                SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = newCluster.Id,
                    signal_event_id = signal.Id,
                    category = signal.Category.ToString().ToLowerInvariant()
                }),
                OccurredAt = now
            }, ct);
            await _civis.EvaluateClusterAsync(newCluster.Id, ct);

            _logger?.LogInformation(
                "{EventName} clusterId={ClusterId} outcome={Outcome} candidateCount={CandidateCount}",
                ObservabilityEvents.SignalRouted, newCluster.Id, "created", candidates.Count);

            _metrics?.SignalJoinOutcomeTotal.Add(
                1,
                new KeyValuePair<string, object?>(
                    SignalsMetrics.TagOutcome,
                    SignalsMetrics.JoinOutcomeCreatedNew));

            return new ClusterRoutingResult(
                newCluster.Id,
                WasCreated: true,
                WasJoined: false,
                ToSnakeCase(newCluster.State),
                newCluster.LocalityId);
        }
    }

    private double ComputeJoinScore(SignalEvent signal, SignalCluster cluster)
    {
        double num = ((signal.SpatialCellId == cluster.SpatialCellId) ? 1.0 : 0.75);
        double totalHours = (DateTime.UtcNow - cluster.LastSeenAt).TotalHours;
        double num2 = Math.Max(0.0, 1.0 - totalHours / _options.TimeScoreMaxAgeHours);
        double num3 = ((!string.IsNullOrEmpty(signal.ConditionSlug) && signal.ConditionSlug == cluster.DominantConditionSlug) ? 1.0 : 0.0);
        return 0.4 + 0.25 * num + 0.2 * num2 + 0.15 * num3;
    }

    private static string ToSnakeCase(SignalState state)
    {
        string pascal = state.ToString();
        var sb = new System.Text.StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static string BuildClusterTitle(SignalEvent signal)
    {
        string text = signal.Category.ToString().ToLowerInvariant();
        string text2 = (string.IsNullOrEmpty(signal.SubcategorySlug) ? "" : (" — " + signal.SubcategorySlug));
        return text + text2;
    }
}

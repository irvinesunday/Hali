using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Clusters;

/// <summary>
/// Evaluates a cluster that is in the <c>possible_restoration</c> state and
/// applies the appropriate lifecycle transition: revert to <c>active</c> if
/// still-affected votes dominate, advance to <c>resolved</c> if the
/// restoration ratio and minimum vote thresholds are met, or take no action
/// if neither condition holds.
///
/// This is one of the four authoritative transition paths that route through
/// <see cref="IClusterRepository.ApplyClusterTransitionAsync"/>. All cluster
/// state changes made by this service are atomic: the cluster row, a
/// <see cref="Hali.Domain.Entities.Clusters.CivisDecision"/>, and an
/// <see cref="Hali.Domain.Entities.Clusters.OutboxEvent"/> are committed
/// together or not at all.
/// </summary>
public interface IRestorationEvaluationService
{
    /// <summary>
    /// Evaluates the cluster identified by <paramref name="clusterId"/>.
    /// No-ops if the cluster does not exist or is not in
    /// <c>possible_restoration</c> state.
    /// </summary>
    Task EvaluateAsync(Guid clusterId, CancellationToken ct = default);
}

using System;

namespace Hali.Application.Clusters;

/// <summary>
/// Structured outcome of routing a signal to a cluster.
/// Returned by <see cref="IClusteringService.RouteSignalAsync"/> so callers
/// know whether a new cluster was created or an existing one was joined.
/// </summary>
public record ClusterRoutingResult(
    Guid ClusterId,
    bool WasCreated,
    bool WasJoined,
    string ClusterState,
    Guid? LocalityId);

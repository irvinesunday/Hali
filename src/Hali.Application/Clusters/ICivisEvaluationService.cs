namespace Hali.Application.Clusters;

public interface ICivisEvaluationService
{
    /// <summary>
    /// Recomputes WRAB/SDS/MACF for the cluster, checks the activation gate,
    /// and transitions unconfirmed → active if gate passes.
    /// Writes a civis_decisions row and emits an outbox event on every state change.
    /// </summary>
    Task EvaluateClusterAsync(Guid clusterId, CancellationToken ct = default);

    /// <summary>
    /// Applies exponential decay to the cluster's live mass.
    /// Transitions active → possible_restoration → resolved when mass drops below threshold.
    /// Writes civis_decisions and outbox events on every state change.
    /// </summary>
    Task ApplyDecayAsync(Guid clusterId, CancellationToken ct = default);
}

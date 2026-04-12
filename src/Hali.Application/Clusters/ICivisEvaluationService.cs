using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Clusters;

public interface ICivisEvaluationService
{
    Task EvaluateClusterAsync(Guid clusterId, CancellationToken ct = default(CancellationToken));

    Task ApplyDecayAsync(Guid clusterId, CancellationToken ct = default(CancellationToken));
}

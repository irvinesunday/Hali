using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Notifications;

public interface INotificationQueueService
{
    /// <summary>Queue cluster_activated notifications for ward followers.</summary>
    Task QueueClusterActivatedAsync(Guid clusterId, Guid? localityId, string title, string summary, CancellationToken ct = default);

    /// <summary>Queue restoration_prompt notifications for previously-affected users.</summary>
    Task QueueRestorationPromptAsync(Guid clusterId, string title, CancellationToken ct = default);

    /// <summary>Queue cluster_resolved notifications for ward followers.</summary>
    Task QueueClusterResolvedAsync(Guid clusterId, Guid? localityId, string title, CancellationToken ct = default);
}

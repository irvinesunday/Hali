using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public interface INotificationRepository
{
    Task EnqueueAsync(Notification notification, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetDueNotificationsAsync(DateTime now, int batchSize, CancellationToken ct = default);
    Task MarkSentAsync(Guid notificationId, DateTime sentAt, CancellationToken ct = default);
    Task MarkFailedAsync(Guid notificationId, CancellationToken ct = default);
}

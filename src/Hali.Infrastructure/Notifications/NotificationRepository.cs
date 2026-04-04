using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Domain.Entities.Notifications;
using Hali.Infrastructure.Data.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Notifications;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationsDbContext _db;

    public NotificationRepository(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(Notification notification, CancellationToken ct = default)
    {
        // Use ON CONFLICT DO NOTHING via dedupe key — skip if already queued
        if (notification.DedupeKey != null)
        {
            bool exists = await _db.Notifications
                .AnyAsync(n => n.DedupeKey == notification.DedupeKey, ct);
            if (exists) return;
        }
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetDueNotificationsAsync(DateTime now, int batchSize, CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.Status == "queued" && n.SendAfter <= now)
            .OrderBy(n => n.SendAfter)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(Guid notificationId, DateTime sentAt, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FindAsync(new object[] { notificationId }, ct);
        if (n != null)
        {
            n.Status = "sent";
            n.SentAt = sentAt;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(Guid notificationId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FindAsync(new object[] { notificationId }, ct);
        if (n != null)
        {
            n.Status = "failed";
            await _db.SaveChangesAsync(ct);
        }
    }
}

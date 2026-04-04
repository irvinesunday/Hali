using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Participation;
using Hali.Domain.Entities.Notifications;

namespace Hali.Application.Notifications;

public class NotificationQueueService : INotificationQueueService
{
    private readonly IFollowRepository _follows;
    private readonly IAuthRepository _auth;
    private readonly IParticipationRepository _participation;
    private readonly INotificationRepository _notifications;

    public NotificationQueueService(
        IFollowRepository follows,
        IAuthRepository auth,
        IParticipationRepository participation,
        INotificationRepository notifications)
    {
        _follows = follows;
        _auth = auth;
        _participation = participation;
        _notifications = notifications;
    }

    public async Task QueueClusterActivatedAsync(Guid clusterId, Guid? localityId, string title, string summary, CancellationToken ct = default)
    {
        if (localityId == null) return;

        var followers = await _follows.GetByLocalityAsync(localityId.Value, ct);
        if (followers.Count == 0) return;

        var accountIds = followers.Select(f => f.AccountId).Distinct().ToList();
        var tokens = await _auth.GetPushTokensByAccountIdsAsync(accountIds, ct);
        await EnqueueForTokensAsync(
            tokens,
            "cluster_activated",
            $"{title} is now active in your ward",
            summary,
            $"cluster_activated:{clusterId}",
            new { cluster_id = clusterId, type = "cluster_activated" },
            ct);
    }

    public async Task QueueRestorationPromptAsync(Guid clusterId, string title, CancellationToken ct = default)
    {
        var accountIds = await _participation.GetAffectedAccountIdsAsync(clusterId, ct);
        if (accountIds.Count == 0) return;

        var tokens = await _auth.GetPushTokensByAccountIdsAsync(accountIds, ct);
        await EnqueueForTokensAsync(
            tokens,
            "restoration_prompt",
            "Has this been resolved?",
            $"The issue '{title}' may be improving. Let us know if it's resolved.",
            $"restoration_prompt:{clusterId}",
            new { cluster_id = clusterId, type = "restoration_prompt" },
            ct);
    }

    public async Task QueueClusterResolvedAsync(Guid clusterId, Guid? localityId, string title, CancellationToken ct = default)
    {
        if (localityId == null) return;

        var followers = await _follows.GetByLocalityAsync(localityId.Value, ct);
        if (followers.Count == 0) return;

        var accountIds = followers.Select(f => f.AccountId).Distinct().ToList();
        var tokens = await _auth.GetPushTokensByAccountIdsAsync(accountIds, ct);
        await EnqueueForTokensAsync(
            tokens,
            "cluster_resolved",
            "Issue resolved",
            $"'{title}' in your ward has been resolved.",
            $"cluster_resolved:{clusterId}",
            new { cluster_id = clusterId, type = "cluster_resolved" },
            ct);
    }

    private async Task EnqueueForTokensAsync(
        IReadOnlyList<string> tokens,
        string notificationType,
        string bodyTitle,
        string bodyText,
        string dedupeKeyBase,
        object payload,
        CancellationToken ct)
    {
        int index = 0;
        foreach (var token in tokens)
        {
            // We don't have per-token account IDs here; use a synthetic account ID
            // The push worker only needs the token, so we embed it in the payload.
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                AccountId = Guid.Empty, // will be resolved by worker from payload
                Channel = "push",
                NotificationType = notificationType,
                Payload = JsonSerializer.Serialize(new
                {
                    expo_push_token = token,
                    title = bodyTitle,
                    body = bodyText,
                    data = payload
                }),
                SendAfter = DateTime.UtcNow,
                Status = "queued",
                DedupeKey = $"{dedupeKeyBase}:{index++}"
            };
            await _notifications.EnqueueAsync(notification, ct);
        }
    }
}

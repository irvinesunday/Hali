using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Notifications;

public record PushMessage(string Token, string Title, string Body, string? Data = null);

public interface IPushNotificationService
{
    Task SendBatchAsync(IEnumerable<PushMessage> messages, CancellationToken ct = default);
}

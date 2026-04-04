using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Domain.Entities.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class SendPushNotificationsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<SendPushNotificationsJob> logger) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "SendPushNotificationsJob pass failed");
            }
        }
    }

    private async Task RunPassAsync(CancellationToken ct)
    {
        logger.LogInformation("{job} {event}", "SendPushNotificationsJob", "start");
        await using var scope = scopeFactory.CreateAsyncScope();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        var now = DateTime.UtcNow;
        var due = await notificationRepo.GetDueNotificationsAsync(now, BatchSize, ct);
        if (due.Count == 0)
        {
            logger.LogInformation("{job} {event} sent=0", "SendPushNotificationsJob", "complete");
            return;
        }

        int sent = 0;
        int failed = 0;
        var messages = new List<(Guid id, PushMessage msg)>();

        foreach (var notification in due)
        {
            var msg = ParsePushMessage(notification);
            if (msg == null)
            {
                await notificationRepo.MarkFailedAsync(notification.Id, ct);
                failed++;
                continue;
            }
            messages.Add((notification.Id, msg));
        }

        if (messages.Count > 0)
        {
            var start = DateTime.UtcNow;
            try
            {
                var pushMessages = new List<PushMessage>();
                foreach (var (_, msg) in messages)
                    pushMessages.Add(msg);

                await pushService.SendBatchAsync(pushMessages, ct);
                var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

                foreach (var (id, _) in messages)
                {
                    await notificationRepo.MarkSentAsync(id, DateTime.UtcNow, ct);
                    sent++;
                }

                logger.LogInformation(
                    "{eventName} sent={Sent} durationMs={DurationMs}",
                    "push.batch_sent", sent, durationMs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push batch send failed; marking {Count} as failed", messages.Count);
                foreach (var (id, _) in messages)
                {
                    await notificationRepo.MarkFailedAsync(id, ct);
                    failed++;
                }
            }
        }

        logger.LogInformation("{job} {event} sent={Sent} failed={Failed}", "SendPushNotificationsJob", "complete", sent, failed);
    }

    private static PushMessage? ParsePushMessage(Notification notification)
    {
        if (string.IsNullOrEmpty(notification.Payload)) return null;
        try
        {
            var doc = JsonDocument.Parse(notification.Payload);
            var root = doc.RootElement;
            string? token = root.TryGetProperty("expo_push_token", out var t) ? t.GetString() : null;
            string? title = root.TryGetProperty("title", out var ti) ? ti.GetString() : null;
            string? body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
            string? data = root.TryGetProperty("data", out var d) ? d.GetRawText() : null;

            if (string.IsNullOrEmpty(token)) return null;
            return new PushMessage(token, title ?? string.Empty, body ?? string.Empty, data);
        }
        catch
        {
            return null;
        }
    }
}

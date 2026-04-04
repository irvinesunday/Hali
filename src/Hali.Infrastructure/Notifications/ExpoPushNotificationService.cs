using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace Hali.Infrastructure.Notifications;

public class ExpoPushNotificationService : IPushNotificationService
{
    private readonly HttpClient _http;
    private readonly ILogger<ExpoPushNotificationService> _logger;
    private const string ExpoSendUrl = "https://exp.host/--/api/v2/push/send";

    public ExpoPushNotificationService(HttpClient http, ILogger<ExpoPushNotificationService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task SendBatchAsync(IEnumerable<PushMessage> messages, CancellationToken ct = default)
    {
        var batch = messages.ToList();
        if (batch.Count == 0) return;

        var payload = batch.Select(m => new
        {
            to = m.Token,
            title = m.Title,
            body = m.Body,
            data = m.Data != null ? JsonSerializer.Deserialize<object>(m.Data) : null,
            sound = "default"
        }).ToList();

        var start = DateTime.UtcNow;
        try
        {
            var response = await _http.PostAsJsonAsync(ExpoSendUrl, payload, ct);
            var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Expo push batch failed {StatusCode} after {DurationMs}ms for {Count} messages",
                    (int)response.StatusCode, durationMs, batch.Count);
                return;
            }

            _logger.LogInformation(
                "Expo push batch sent {Count} messages in {DurationMs}ms",
                batch.Count, durationMs);
        }
        catch (Exception ex)
        {
            var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;
            _logger.LogError(ex,
                "Expo push batch threw after {DurationMs}ms for {Count} messages",
                durationMs, batch.Count);
            throw;
        }
    }
}

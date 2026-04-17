using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Microsoft.Extensions.Logging;

namespace Hali.Infrastructure.Notifications;

public class ExpoPushNotificationService : IPushNotificationService
{
    private readonly HttpClient _http;
    private readonly ILogger<ExpoPushNotificationService> _logger;
    private readonly PushNotificationsMetrics _metrics;
    private const string ExpoSendUrl = "https://exp.host/--/api/v2/push/send";

    public ExpoPushNotificationService(
        HttpClient http,
        ILogger<ExpoPushNotificationService> logger,
        PushNotificationsMetrics metrics)
    {
        _http = http;
        _logger = logger;
        _metrics = metrics;
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

        // Start the stopwatch as late as possible so the histogram reflects
        // only the outbound HTTP segment — the portion whose latency depends
        // on Expo rather than on our serialisation or CLR startup costs.
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        try
        {
            response = await _http.PostAsJsonAsync(ExpoSendUrl, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                sw.Stop();
                var durationMs = sw.Elapsed.TotalMilliseconds;
                // HTTP 4xx (auth / bad request) is not recoverable by retry;
                // 5xx (and anything else non-2xx) is treated as transient so
                // operators can distinguish upstream brokenness from
                // upstream misconfiguration at a glance.
                var batchOutcome = IsClientError(response.StatusCode)
                    ? PushNotificationsMetrics.OutcomePermanentError
                    : PushNotificationsMetrics.OutcomeTransientError;

                RecordBatchFailure(batch.Count, batchOutcome, sw.Elapsed.TotalSeconds);

                _logger.LogWarning(
                    "Expo push batch failed {StatusCode} after {DurationMs}ms for {Count} messages",
                    (int)response.StatusCode, durationMs, batch.Count);
                return;
            }

            // 2xx — parse the response body to classify each message. If
            // the body is unparseable, fall back to counting every message
            // as success (matching the pre-metric behaviour, which treated
            // any 2xx as "sent") rather than silently losing the attempts.
            // The fallback path is unusual enough that emitting a warning
            // below is the right default — it lets operators spot upstream
            // contract drift (Expo response shape changed) without needing
            // to grep verbose debug logs.
            string responseBody = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();
            var successDurationSeconds = sw.Elapsed.TotalSeconds;
            var successDurationMs = sw.Elapsed.TotalMilliseconds;

            bool usedFallback = RecordPerMessageOutcomes(batch.Count, responseBody);
            _metrics.PushSendDuration.Record(
                successDurationSeconds,
                new KeyValuePair<string, object?>(
                    PushNotificationsMetrics.TagOutcome,
                    PushNotificationsMetrics.OutcomeSuccess));

            if (usedFallback)
            {
                // Warning (not error) because the pre-metric contract
                // still holds — every message counts as sent on any 2xx
                // — but the classifier could not extract structured
                // per-ticket dispositions from the response. Repeated
                // occurrences suggest upstream drift worth investigating.
                _logger.LogWarning(
                    "Expo push batch 2xx response was unparseable or lacked a data[] array; counted {Count} messages as success in {DurationMs}ms",
                    batch.Count, successDurationMs);
            }
            else
            {
                _logger.LogInformation(
                    "Expo push batch sent {Count} messages in {DurationMs}ms",
                    batch.Count, successDurationMs);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // True caller cancellation (host shutdown / aborted worker pass).
            // Mirrors SignalsMetrics: excluded from the outcome taxonomy so
            // disconnects do not bias any counter bucket.
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var durationMs = sw.Elapsed.TotalMilliseconds;
            RecordBatchFailure(batch.Count, PushNotificationsMetrics.OutcomeTransientError, sw.Elapsed.TotalSeconds);

            _logger.LogError(ex,
                "Expo push batch threw after {DurationMs}ms for {Count} messages",
                durationMs, batch.Count);
            throw;
        }
        finally
        {
            response?.Dispose();
        }
    }

    /// <summary>
    /// Emits one <c>push_send_attempts_total</c> measurement per message in
    /// the batch and one <c>push_send_duration_seconds</c> measurement for
    /// the whole batch, all tagged with the same batch-level outcome. Used
    /// on HTTP non-2xx and transport-exception paths where Expo never
    /// returned structured per-message dispositions.
    /// </summary>
    private void RecordBatchFailure(int messageCount, string outcome, double durationSeconds)
    {
        var tag = new KeyValuePair<string, object?>(PushNotificationsMetrics.TagOutcome, outcome);
        _metrics.PushSendAttemptsTotal.Add(messageCount, tag);
        _metrics.PushSendDuration.Record(durationSeconds, tag);
    }

    /// <summary>
    /// Parses an Expo success response (<c>{"data":[{"status":"ok"|"error",
    /// "details":{"error":"..."}}]}</c>) and emits one
    /// <c>push_send_attempts_total</c> measurement per distinct outcome
    /// bucket (with the per-bucket count rolled up before emission, so a
    /// mixed batch yields one measurement per bucket, not one per ticket).
    /// If the body cannot be parsed or no <c>data</c> array is present,
    /// falls back to counting every request-side message as
    /// <see cref="PushNotificationsMetrics.OutcomeSuccess"/> — this matches
    /// the pre-metric contract (any 2xx counted as sent) while keeping
    /// the counter honest about attempts.
    ///
    /// <para>
    /// <b>Length mismatch policy.</b> When Expo returns a <c>data</c>
    /// array whose length differs from the request-side message count,
    /// this method emits one increment per actual <c>data</c> element
    /// rather than falling back to an all-<c>success</c> rollup. Expo's
    /// documented contract is one element per request message, so a
    /// length mismatch already indicates upstream misbehaviour; reporting
    /// the dispositions Expo actually returned is more informative than
    /// hiding them behind a blanket success. The unparseable-body
    /// fallback above still applies whenever the response isn't JSON or
    /// lacks a <c>data</c> array entirely.
    /// </para>
    /// </summary>
    /// <returns>
    /// <c>true</c> if the parser had to fall back to the
    /// all-<c>success</c> path (body unparseable / no <c>data</c> array);
    /// <c>false</c> if at least one ticket was classified from the
    /// response body. The caller uses this to decide whether to log a
    /// warning about upstream contract drift.
    /// </returns>
    private bool RecordPerMessageOutcomes(int requestCount, string responseBody)
    {
        var counts = new Dictionary<string, long>(capacity: 4);

        bool parsedSuccessfully = false;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in data.EnumerateArray())
                {
                    var outcome = ClassifyExpoTicket(element);
                    counts[outcome] = counts.GetValueOrDefault(outcome) + 1;
                }
                parsedSuccessfully = counts.Count > 0;
            }
        }
        catch (JsonException)
        {
            // Fall through to the unparseable path below.
        }

        if (!parsedSuccessfully)
        {
            // Either the response wasn't JSON, had no data array, or had an
            // empty data array — record every request-side message as
            // success so the counter stays truthful about attempts.
            counts[PushNotificationsMetrics.OutcomeSuccess] = requestCount;
        }

        foreach (var (outcome, count) in counts)
        {
            _metrics.PushSendAttemptsTotal.Add(
                count,
                new KeyValuePair<string, object?>(PushNotificationsMetrics.TagOutcome, outcome));
        }

        return !parsedSuccessfully;
    }

    /// <summary>
    /// Maps a single Expo ticket element to one of the four bounded outcome
    /// buckets. Unknown or missing error codes fall through to
    /// <see cref="PushNotificationsMetrics.OutcomeTransientError"/> — the
    /// safe default for "might succeed on a later attempt".
    /// </summary>
    private static string ClassifyExpoTicket(JsonElement ticket)
    {
        if (ticket.ValueKind != JsonValueKind.Object)
        {
            return PushNotificationsMetrics.OutcomeTransientError;
        }

        string? status = ticket.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : null;

        if (string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return PushNotificationsMetrics.OutcomeSuccess;
        }

        // Anything that isn't "ok" is an error — read details.error to
        // decide the sub-bucket.
        string? errorCode = null;
        if (ticket.TryGetProperty("details", out var details)
            && details.ValueKind == JsonValueKind.Object
            && details.TryGetProperty("error", out var errElement)
            && errElement.ValueKind == JsonValueKind.String)
        {
            errorCode = errElement.GetString();
        }

        return errorCode switch
        {
            "DeviceNotRegistered" => PushNotificationsMetrics.OutcomeDeviceInvalid,
            "InvalidCredentials" => PushNotificationsMetrics.OutcomePermanentError,
            "MessageTooBig" => PushNotificationsMetrics.OutcomePermanentError,
            // MessageRateExceeded and any unrecognised error code are both
            // treated as transient — the message may succeed if retried.
            _ => PushNotificationsMetrics.OutcomeTransientError,
        };
    }

    private static bool IsClientError(HttpStatusCode status) =>
        (int)status >= 400 && (int)status < 500;
}

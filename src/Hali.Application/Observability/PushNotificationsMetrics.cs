using System;
using System.Diagnostics.Metrics;

namespace Hali.Application.Observability;

/// <summary>
/// Hosts the <c>Hali.Notifications</c> <see cref="Meter"/> and the instruments
/// covering the Phase 1 push-notification path (R3.d, issue #170). Restoration
/// prompts and new-cluster-in-followed-ward pushes are the only user-visible
/// asynchronous output the citizen mobile app produces, and before this meter
/// the <see cref="Hali.Application.Notifications.IPushNotificationService"/>
/// pipeline was visible only through the structured log stream — which is fine
/// for incident forensics but not queryable as a time series for pilot alerting.
///
/// <list type="bullet">
///   <item><description><c>push_send_attempts_total</c> — one increment per
///     <see cref="Hali.Application.Notifications.PushMessage"/> submitted to
///     Expo, tagged by the per-message outcome derived from the Expo response
///     (<c>status</c> + <c>details.error</c>) or, when the batch fails as a
///     whole, from the HTTP status / transport exception. Emitted from
///     <c>ExpoPushNotificationService.SendBatchAsync</c> — the only layer
///     that sees the real per-message disposition.</description></item>
///   <item><description><c>push_send_duration_seconds</c> — latency histogram
///     covering exactly the outbound HTTP call to
///     <c>https://exp.host/--/api/v2/push/send</c> (request send +
///     response header read + body parse). Tagged with a batch-level outcome
///     bucket so the "upstream slow" vs "upstream broken" distinction stays
///     visible without reaching for trace data.</description></item>
///   <item><description><c>push_token_registrations_total</c> — one increment
///     per successful <c>POST /v1/devices/push-token</c> call, tagged with
///     the registration <c>result</c> (<c>new</c>, <c>updated</c>, or
///     <c>unchanged</c>). Emitted from <c>DevicesController</c> because that
///     is the only layer with both the previously-persisted token (on the
///     resolved <c>Device</c> entity) and the new token from the request
///     body.</description></item>
/// </list>
///
/// The meter is registered on the OpenTelemetry meter provider in
/// <c>Program.cs</c> under the name <see cref="MeterName"/>. When
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is unset the meter and its instruments
/// still exist in-process (zero-cost, non-exported) and no behaviour regresses.
///
/// The class lives in <c>Hali.Application.Observability</c> (parallel to
/// <see cref="SignalsMetrics"/> / <see cref="ClustersMetrics"/>) because the
/// instruments are emitted from both the API tier (<c>DevicesController</c>)
/// and the Infrastructure tier (<c>ExpoPushNotificationService</c>). The
/// Application project is the lowest common reference point.
///
/// Tag values are bounded by static catalogs — no push token, device id,
/// account id, locality id, notification title/body, correlation id,
/// idempotency key, or Expo message id is ever attached. At steady state the
/// three instruments produce at most 4 + 3 + 3 = <b>10</b> time series.
/// </summary>
public sealed class PushNotificationsMetrics : IDisposable
{
    /// <summary>
    /// Name of the push-notifications <see cref="Meter"/>. Mirrored by
    /// <c>AddMeter(PushNotificationsMetrics.MeterName)</c> on the
    /// OpenTelemetry meter provider so instruments export through the existing
    /// OTLP transport.
    /// </summary>
    public const string MeterName = "Hali.Notifications";

    /// <summary>Counter name — per-message push send attempts.</summary>
    public const string PushSendAttemptsTotalName = "push_send_attempts_total";

    /// <summary>Histogram name — Expo push HTTP call duration in seconds.</summary>
    public const string PushSendDurationName = "push_send_duration_seconds";

    /// <summary>Counter name — successful push-token registrations.</summary>
    public const string PushTokenRegistrationsTotalName = "push_token_registrations_total";

    // ── Tag keys ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tag key on <see cref="PushSendAttemptsTotal"/> and
    /// <see cref="PushSendDuration"/> identifying the operational outcome of
    /// the send. Values for the counter are drawn from
    /// <see cref="OutcomeSuccess"/>, <see cref="OutcomeDeviceInvalid"/>,
    /// <see cref="OutcomeTransientError"/>, <see cref="OutcomePermanentError"/>.
    /// The histogram uses the same key but only the three batch-visible
    /// dispositions (<c>success</c>, <c>transient_error</c>,
    /// <c>permanent_error</c>) — <c>device_invalid</c> is per-message and
    /// never appears on the histogram because the HTTP call succeeded.
    /// </summary>
    public const string TagOutcome = "outcome";

    /// <summary>
    /// Tag key on <see cref="PushTokenRegistrationsTotal"/> identifying
    /// whether the request registered a new token, updated a different
    /// existing token, or replayed the same token already on file. Values:
    /// <see cref="ResultNew"/>, <see cref="ResultUpdated"/>,
    /// <see cref="ResultUnchanged"/>.
    /// </summary>
    public const string TagResult = "result";

    // ── Send outcome tag values ─────────────────────────────────────────────
    // Bounded four-way classification derived from Expo's documented response
    // shape (https://docs.expo.dev/push-notifications/sending-notifications).
    // Each per-message disposition maps to exactly one bucket:
    //
    //   status == "ok"                                               → success
    //   status == "error", details.error == "DeviceNotRegistered"    → device_invalid
    //   status == "error", details.error == "InvalidCredentials"     → permanent_error
    //   status == "error", details.error == "MessageTooBig"          → permanent_error
    //   status == "error", details.error == "MessageRateExceeded"    → transient_error
    //   status == "error", any other details.error or unparseable    → transient_error
    //
    // For batch-level failures (HTTP non-2xx, network exception, server-side
    // timeout) every message in the batch is counted under the batch outcome:
    //
    //   HTTP 5xx or transport exception or server-side timeout       → transient_error
    //   HTTP 4xx (InvalidCredentials-class auth/bad-request)         → permanent_error
    //
    // Client-driven cancellation (caller CT signaled) is not a send outcome
    // — no counter or histogram measurement is recorded in that case, which
    // mirrors the guard already established by SignalsMetrics.

    /// <summary>Outcome: Expo acknowledged the message (<c>status=="ok"</c>).</summary>
    public const string OutcomeSuccess = "success";

    /// <summary>
    /// Outcome: Expo reported <c>DeviceNotRegistered</c>. The token is dead
    /// and the upstream notification repo should stop targeting it (existing
    /// "failed" marking in <c>SendPushNotificationsJob</c> is preserved — this
    /// bucket just makes the count queryable).
    /// </summary>
    public const string OutcomeDeviceInvalid = "device_invalid";

    /// <summary>
    /// Outcome: Expo reported a transient error (<c>MessageRateExceeded</c>,
    /// any unknown error code, HTTP 5xx, network/transport exception, or
    /// server-side timeout). Safe default for any disposition that might
    /// succeed on a later attempt.
    /// </summary>
    public const string OutcomeTransientError = "transient_error";

    /// <summary>
    /// Outcome: Expo reported a permanent error that will not recover on
    /// retry without operator intervention — <c>InvalidCredentials</c>,
    /// <c>MessageTooBig</c>, or an HTTP 4xx batch failure.
    /// </summary>
    public const string OutcomePermanentError = "permanent_error";

    // ── Token-registration result tag values ────────────────────────────────
    // Derived by comparing the newly-submitted token to the value already
    // persisted on the resolved Device (returned by
    // FindDeviceByFingerprintAsync):
    //
    //   device.ExpoPushToken == null                     → new
    //   device.ExpoPushToken == request.ExpoPushToken    → unchanged
    //   device.ExpoPushToken != request.ExpoPushToken    → updated
    //
    // Three-bucket catalog is deliberately tiny — the tag's operational job
    // is to let operators see churn (updated-rate spikes typically indicate
    // OS token rotation or app reinstalls) and to validate that no-op
    // heartbeat traffic isn't dominating.

    /// <summary>Result: first push token registration for this device.</summary>
    public const string ResultNew = "new";

    /// <summary>Result: device had a prior push token that was replaced.</summary>
    public const string ResultUpdated = "updated";

    /// <summary>Result: device already had this exact token — no semantic change.</summary>
    public const string ResultUnchanged = "unchanged";

    private readonly Meter _meter;

    /// <summary>
    /// <c>push_send_attempts_total</c> — incremented once per
    /// <see cref="Hali.Application.Notifications.PushMessage"/> submitted in a
    /// batch to Expo, tagged with the per-message
    /// <see cref="TagOutcome"/>. Emitted from
    /// <c>ExpoPushNotificationService.SendBatchAsync</c> because that is the
    /// only layer that observes both the outgoing <c>PushMessage</c> list
    /// and the Expo response body (or transport failure) that classifies
    /// each message. No push token, Expo message id, batch id, device id,
    /// account id, or notification title/body is ever attached.
    ///
    /// For a successful HTTP call the counter emits one measurement per
    /// response <c>data[]</c> element keyed to its
    /// <c>status</c>/<c>details.error</c>. For a whole-batch failure (HTTP
    /// non-2xx, transport exception, server-side timeout) the counter emits
    /// one measurement per request-side <c>PushMessage</c> with the
    /// batch-level outcome — this keeps the counter truthful as a measure
    /// of "messages attempted" even when Expo never returned structured
    /// per-message dispositions.
    /// </summary>
    public Counter<long> PushSendAttemptsTotal { get; }

    /// <summary>
    /// <c>push_send_duration_seconds</c> — latency histogram covering the
    /// outbound Expo send call. The recorded span starts immediately before
    /// <c>PostAsJsonAsync</c> and ends after the response body has been read
    /// and parsed (the only portion of the worker pass whose latency
    /// depends on upstream health). It deliberately does not include queue
    /// fetch, payload parse, or database mark-as-sent work — those are
    /// owned by the caller path and measured separately by the worker's
    /// own structured logs.
    ///
    /// Tag:
    /// <list type="bullet">
    ///   <item><description><see cref="TagOutcome"/> —
    ///     <see cref="OutcomeSuccess"/> |
    ///     <see cref="OutcomeTransientError"/> |
    ///     <see cref="OutcomePermanentError"/>. The histogram never emits
    ///     <see cref="OutcomeDeviceInvalid"/> because that is a per-message
    ///     disposition observed on an otherwise-successful HTTP call.</description></item>
    /// </list>
    /// </summary>
    public Histogram<double> PushSendDuration { get; }

    /// <summary>
    /// <c>push_token_registrations_total</c> — incremented once per
    /// successful <c>POST /v1/devices/push-token</c> call, tagged with
    /// <see cref="TagResult"/>. Emitted from
    /// <c>DevicesController.RegisterPushToken</c> after the persist call
    /// returns, so the counter observes the real post-write disposition
    /// (no increment is emitted for validation failures or
    /// device-not-found responses — those are already covered by the
    /// API-wide exception counter).
    ///
    /// No device hash, push token value, account id, or correlation id is
    /// ever attached.
    /// </summary>
    public Counter<long> PushTokenRegistrationsTotal { get; }

    public PushNotificationsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        PushSendAttemptsTotal = _meter.CreateCounter<long>(
            name: PushSendAttemptsTotalName,
            unit: "{message}",
            description: "Number of push messages submitted to Expo, tagged by per-message send outcome.");

        PushSendDuration = _meter.CreateHistogram<double>(
            name: PushSendDurationName,
            unit: "s",
            description: "Duration of the Expo push send HTTP call, tagged by batch-level outcome.");

        PushTokenRegistrationsTotal = _meter.CreateCounter<long>(
            name: PushTokenRegistrationsTotalName,
            unit: "{registration}",
            description: "Number of successful push-token registrations, tagged by new/updated/unchanged result.");
    }

    public void Dispose() => _meter.Dispose();
}

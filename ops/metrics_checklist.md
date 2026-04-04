# Hali Metrics Instrumentation Checklist

Status of custom metrics required by SLO and alert definitions.

## Currently Emitted (via OpenTelemetry)

| Metric | Source | Status |
|--------|--------|--------|
| `http_request_duration_ms` | ASP.NET Core OTel instrumentation | Emitted |
| `http_requests_total` | ASP.NET Core OTel instrumentation | Emitted |

## Needs Implementation

These metrics are referenced by SLOs and alert rules but are not yet emitted.
Add them using `System.Diagnostics.Metrics` (already available via OTel integration).

| Metric | Type | Source | Priority |
|--------|------|--------|----------|
| `hali_db_health_check` | Gauge (0/1) | Health check endpoint | P1 |
| `hali_redis_health_check` | Gauge (0/1) | Health check endpoint | P1 |
| `hali_queue_depth{queue}` | Gauge | Outbox query (`WHERE published_at IS NULL`) | P2 |
| `hali_dead_letter_queue_depth` | Gauge | DLQ table count | P2 |
| `hali_otp_delivery_attempted_total` | Counter | `OtpService.RequestOtpAsync` | P2 |
| `hali_otp_delivery_failed_total` | Counter | `ISmsProvider` error handler | P2 |
| `hali_notifications_attempted_total` | Counter | `SendPushNotificationsJob` | P2 |
| `hali_notifications_delivered_total` | Counter | `IPushNotificationService` success | P2 |
| `hali_burst_anomaly_score` | Gauge | CIVIS scoring worker (future) | P3 |
| `hali_jurisdiction_violation_total` | Counter | `OfficialPostsService` 403 path | P3 |
| `hali_projection_mismatch_total` | Counter | Projection rebuild job (future) | P3 |
| `hali_loop_closure_rate` | Gauge | Periodic metrics job (future) | P4 |
| `hali_cluster_activation_rate` | Gauge | Periodic metrics job (future) | P4 |

## Implementation Approach

1. Create a `HaliMetrics` static class in `Hali.Application` with `Meter` and all counter/gauge definitions
2. Register the meter in DI via `builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("Hali"))`
3. Inject and increment counters at the listed source locations
4. P1/P2 metrics should be added before production launch
5. P3/P4 metrics can be deferred to post-launch iteration

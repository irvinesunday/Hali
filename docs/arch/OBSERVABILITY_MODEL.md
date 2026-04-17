# Hali Observability and Instrumentation Model

Canonical observability model for Hali across backend (API + workers), citizen
mobile, and institution web. This document defines **what** we instrument
and **how** — naming, payload rules, required signals per surface, and the
pipeline those signals flow through.

It is a policy document. Concrete event constants live in
`src/Hali.Application/Observability/ObservabilityEvents.cs`. Concrete SLOs
live in `ops/slos/slo_definitions.yaml`. Concrete alert rules live in
`ops/alerts/alert_rules.yaml`. Runbooks live in `docs/runbooks/`.

When a surface needs an event, metric, or SLO that is not in this model,
add the rule here first, then add the concrete constant / rule / entry.
Don't ship drive-by telemetry.

Authority hierarchy: this document sits below `CLAUDE.md`,
`Hali_Platform_Reconciliation_v1.md`, and `SECURITY_POSTURE.md`, and above
the surface-specific instrumentation code files.

---

## 1. Principles

1. **Signals over noise.** Every event, metric, or span has a reader who
   will act on it. If nobody would act, don't emit it.
2. **Wire what changes decisions.** Backend state transitions, cascading
   failure modes, rate limits, and auth outcomes. Not debug prints.
3. **PII-safe by default.** Every event that carries user-derived data
   runs through `ObservabilityEvents.SanitizeForLog` or equivalent, and
   the posture rules in `SECURITY_POSTURE.md` §4 take precedence.
4. **Cardinality bounded.** No unbounded tag values. No free-text. No
   request path with path parameters expanded.
5. **Name once, emit many.** Event names are defined as constants in
   `ObservabilityEvents.cs`. Never use an event name literal at the
   emission site.
6. **Replayable.** Every state-changing backend write emits an outbox
   event in the same transaction. Observability is additive; it never
   replaces the outbox.

---

## 2. Signal types

Hali emits four kinds of telemetry. Do not confuse them.

| Type | Purpose | Transport | Retention |
|---|---|---|---|
| Structured log | High-cardinality operational context; one row per interesting thing | Serilog → stdout / log aggregator | 30 days |
| Metric | Low-cardinality counter / histogram for SLOs, alerts, dashboards | OpenTelemetry → OTLP endpoint (Prometheus-compatible) | 13 months (rolling) |
| Distributed trace | Request-scoped span graph for latency attribution | OpenTelemetry → OTLP | 7 days |
| Audit record | Permanent append-only record of privileged action | Dedicated DB table (`admin_audit_logs` etc.) + outbox | Policy-bounded (never deleted by ops) |

An event can produce more than one signal — e.g. `cluster.activated`
emits a structured log *and* increments a metric *and* writes an outbox
event. The rules for each transport still apply independently.

---

## 3. Backend telemetry model

### 3.1 API-tier signals

Every authenticated HTTP handler emits, at minimum:

- Structured log at request start and completion, with `correlation_id`,
  `route_name` (never raw path), `actor_type` (`citizen` / `institution` /
  `institution_admin` / `anonymous`), and outcome.
- HTTP request duration histogram tagged by `route_name`, `status_code`,
  `method`. Handled by the ASP.NET Core OpenTelemetry instrumentation.
- An error counter (`api_exceptions_total` — see
  `ApiMetrics.ApiExceptionsTotalName`) tagged by `error_code`,
  `error_category`, `status_code`. Internal-only error codes are
  redacted to `server.internal_error` before tagging to match the wire.

Route-specific instrumentation is factored out into `*Metrics` classes
in `src/Hali.Api/Observability/` and `src/Hali.Application/Observability/`:

- `HomeMetrics` — home feed latency, cache hit ratio, section build time.
- `SignalsMetrics` — preview NLP latency, submit throughput, join rate,
  locality resolution outcome.
- `ClustersMetrics` — participation event counters, lifecycle transition
  counters, restoration ratio snapshots.
- `PushNotificationsMetrics` — push dispatch success / failure / latency.
- `ApiMetrics` — exception counter, rate-limit trigger counter.

### 3.2 Worker-tier signals

Every worker job emits:

- Structured log at job pickup, success, and failure — with
  `job_type`, `attempt`, `queue`, `correlation_id`, and outcome.
- A worker-throughput counter per `job_type`.
- A worker-lag gauge per `queue` (age of oldest pending item).
- A dead-letter counter per `job_type` when a job exceeds its retry
  budget.

Failure paths use the same `error_category` vocabulary the API uses.

### 3.3 Auth failures

Auth outcomes are a first-class signal because they are both a security
signal and a UX pain signal.

Required:

- `auth.otp.request` — start, success, failure (with `failure_category`
  bucketed: `rate_limited`, `destination_invalid`, `sms_provider_failed`)
- `auth.otp.verify` — start, success, failure (buckets: `wrong_code`,
  `expired`, `attempts_exhausted`)
- `auth.refresh` — start, success, failure (buckets: `expired`,
  `revoked`, `theft_detected`, `unknown_token`)
- `auth.login.web` — (planned, per #197) start, success, failure
- `auth.totp.verify` — (planned, per #197) start, success, failure

Every auth failure increments a dedicated counter so alerts can threshold
on failure rate without colliding with generic 4xx counters.

### 3.4 Cluster lifecycle transitions

Every cluster state transition emits all three signals:

| Transition | Event name | Required tags |
|---|---|---|
| Unconfirmed → Active (CIVIS activation) | `cluster.activated` | `cluster_id`, `locality_id`, `category`, `reason_code` |
| Active → Possible Restoration | `cluster.possible_restoration` | `cluster_id`, `trigger` (`official` / `citizen_vote`) |
| Possible Restoration → Resolved | `cluster.restoration_confirmed` | `cluster_id`, `ratio`, `affected_vote_count` |
| Possible Restoration → Active (reverted) | `cluster.reverted_to_active` | `cluster_id`, `reason_code` |
| Active / Possible Restoration → Resolved (decay) | `cluster.resolved_by_decay` | `cluster_id`, `age_hours` |

**CIVIS internals (`civis_score`, `wrab`, `sds`, `macf`,
`raw_confirmation_count`) never leave the backend in any observability
signal emitted to an external collector.** These are internal decision
inputs. If a gauge of CIVIS scores is useful for on-call, it stays in
internal-only dashboards.

### 3.5 Official-post creation

- `official.post.created` — `post_type` (`live_update` /
  `scheduled_disruption` / `advisory_public_notice`), `institution_id`,
  `target_locality_id`, `cluster_id` if bound.
- `official.post.rejected` — with `reason_code` when jurisdiction or
  scope checks fail.
- Dedicated rate-limit counter (if/when rate limiting is added, per
  `SECURITY_POSTURE.md` §8).

### 3.6 Restoration actions

- `restoration.response.recorded` — `cluster_id`, `vote` (`yes` /
  `still_affected`).
- `restoration.ratio.snapshot` — gauge sampled on each response.
- `restoration.window.expired` — emitted when the restoration window
  closes without enough affected-vote confirmation.

### 3.7 Dependencies and external calls

External-service calls (Africa's Talking SMS, Anthropic NLP, Expo Push,
Nominatim geocoding) each emit:

- Outbound latency histogram.
- Outbound error counter bucketed by vendor error taxonomy (do not pass
  raw vendor strings through as tags).
- Circuit state (`closed` / `open` / `half_open`) when a breaker is in
  use.

---

## 4. Citizen mobile telemetry

Mobile telemetry is both a product signal and a pain signal. The rules
above apply to the mobile client as well, with extra constraints on
what can be reported upstream.

### 4.1 Required events

- App-lifecycle: cold start, warm start, background, foreground.
- Auth flow: OTP request, OTP verify, refresh (on-device rotation
  success / failure).
- Signal composer: NLP preview latency (wall-clock, device-side),
  submit success / failure, offline queue depth on entry and exit.
- Home feed: time-to-first-render, cache hit (served from cache vs
  network), empty-state vs content outcome.
- Participation: affected tap, observing tap, no-longer-affected tap
  — success / failure.
- Navigation events: screen shown (bounded set of screen names, not
  free-text routes).

### 4.2 Prohibited payloads

- Phone number (raw or partial)
- OTP code
- Device identifier raw (use a per-install install_id hash — see
  `apps/citizen-mobile/src/lib/install-id.ts` for the canonical
  helper once introduced)
- Free-text signal content
- GPS coordinates beyond the ward resolution level

### 4.3 Failure capture

- Unhandled JS exceptions and native crashes are captured and reported
  with:
  - Screen name
  - Install id (not account id)
  - App version + build
  - Redacted stack trace (no user-supplied strings)
- Network failures are not user data — they can be reported verbatim
  including HTTP status and `error_code` from the server envelope.

### 4.4 Transport

Implementation is scheduled in #206 (client-side instrumentation for core
citizen mobile flows) and #208 (front-end error capture). Until then,
opt-out client instrumentation does not bypass these rules — a
not-yet-wired event still has a canonical name reserved here.

---

## 5. Institution web telemetry

Implementation is scheduled in #207 (observability hooks and event
taxonomy for institution web MVP) and #208 (shared front-end error
capture). Institution web must ship with these signals from day one,
not as a follow-up.

### 5.1 Required events

- Auth flow: magic link request, magic link verify, TOTP verify,
  step-up-auth challenge.
- Dashboard shell: route entered (bounded set of route names),
  time-to-interactive.
- Live Signals board: time-to-first-render, live-refresh cadence,
  WebSocket / SSE connection status.
- Official update flow: update drafted, update posted, update
  cancelled, schedule modified.
- Restoration action: action initiated, action submitted, action
  rejected by server.
- Jurisdiction / scope boundary hits: every 403 from the API that
  indicates out-of-scope attempted access — valuable both as a UX
  signal ("why did my action fail") and a security signal (repeated
  hits = misconfigured scope or hostile actor).

### 5.2 Prohibited payloads

- Citizen account id, device id, phone number, or free-text signal
  content
- Raw email addresses of other institution users
- TOTP secrets or step-up-auth challenge values

### 5.3 Failure capture

- Unhandled JS exceptions captured with route name, actor role,
  institution id (not user id), app version, and redacted stack trace.

---

## 6. Event naming rules

- **Dot-delimited, lowercase, snake_case segments.**
  `cluster.activated`, `signal.submit.failed`, `auth.otp.request`.
- **Segment order: subject → verb → outcome.**
  `home.request.started`, not `started.home.request`.
- **Outcomes are one of:** `started`, `completed`, `failed`, `created`,
  `joined`, `confirmed`, `reverted`, `rejected`, `resolved`,
  `snapshot`, `recorded`. If a new outcome word is needed, add it to
  this list.
- **Never encode values in the event name.** Tag them instead.
  `cluster.activated` with tag `category=roads`, not
  `cluster.activated.roads`.
- **Never rename a live event.** Add a new one and deprecate the old
  one in place.

---

## 7. Payload discipline

### 7.1 Log templates

- Structured log template values are const string literals in C# code —
  never built via string concatenation with user input.
- User-supplied values that appear in templates are sanitised via
  `ObservabilityEvents.SanitizeForLog(value)`.
- The route identifier in logs is `context.GetEndpoint()?.DisplayName`
  — never `Request.Path`. CodeQL flags the raw-path pattern repeatedly
  (see `docs/arch/COPILOT_LESSONS.md`).

### 7.2 Metric tag cardinality

- Tag values come from a bounded enumeration. Examples:
  - `route_name` → OpenTelemetry normalised route template
  - `category` → the eight canonical `CivicCategory` values
  - `actor_type` → `citizen` / `institution` / `institution_admin` /
    `hali_ops` / `anonymous`
  - `reason_code` → the reason-code catalog in
    `Hali.Application.Errors.ReasonCodes` / equivalent
- Tags for `cluster_id`, `account_id`, `device_id`, and free-text are
  **never** applied to metrics. They belong in logs and traces only.

### 7.3 Trace attributes

- Same bounded-value rule as metrics.
- Always include `correlation_id` if one is present on the request.
- Never include secrets, tokens, or raw PII, even in a span attribute
  — spans leave the process boundary just like logs.

---

## 8. SLOs and alerts

SLOs are defined in `ops/slos/slo_definitions.yaml` and reviewed every
30 days against production traffic.

Current SLO coverage:

- API availability (non-5xx responses over 30 days)
- Latency SLOs per critical route (`/v1/signals/preview`,
  `/v1/signals/submit`, `/v1/auth/*`, `/v1/home`,
  `/v1/clusters/{id}/*`)
- Worker throughput and lag

Alert rules are defined in `ops/alerts/alert_rules.yaml` and must each
reference a runbook in `docs/runbooks/`. Every alert without a runbook
is a process failure — fix the gap or delete the alert.

Alert severity:

- **P1** — immediate response (pager); user-facing outage or security
  alarm.
- **P2** — same-business-day response; degraded but still serving.
- **P3** — triaged during ops hours; trending concern.
- **P4** — informational; review in the weekly ops huddle.

---

## 9. Pipeline

```
┌──────────────────┐     ┌─────────────────┐     ┌──────────────────┐
│ Serilog (stdout) │     │ OTel SDK        │     │ Sentry / crash   │
│ per-process      │────▶│ (API + Workers) │────▶│ reporter (client) │
└──────────────────┘     └────────┬────────┘     └──────────────────┘
                                  │
                                  ▼
                         ┌─────────────────┐
                         │ OTLP collector  │  (OTEL_EXPORTER_OTLP_ENDPOINT)
                         └────────┬────────┘
                                  │
                 ┌────────────────┼────────────────┐
                 ▼                ▼                ▼
          ┌────────────┐   ┌────────────┐   ┌────────────┐
          │ Metrics    │   │ Traces     │   │ Logs       │
          │ (Prom)     │   │ (Jaeger/   │   │ (Loki/     │
          │            │   │ Tempo)     │   │ OpenSearch)│
          └─────┬──────┘   └────────────┘   └────────────┘
                ▼
          ┌────────────┐
          │ Grafana    │ (dashboards + SLO burn-rate alerts)
          └────────────┘
```

When `OTEL_EXPORTER_OTLP_ENDPOINT` is unset (local development), the
SDK instruments stay in-process at zero cost and no behaviour
regresses — see `ApiMetrics.cs` for the pattern.

Front-end crash reporting target is Sentry (or equivalent) — the
specific provider and DSN handling are out of scope here; see
`SECURITY_POSTURE.md` §4 for PII-redaction rules that apply regardless
of provider.

---

## 10. Known gaps

| Gap | Target | Tracking |
|---|---|---|
| Client-side instrumentation for citizen mobile | All §4 events wired | #206 |
| Institution web observability | All §5 events wired at launch | #207 |
| Front-end error capture | Unified crash pipeline for mobile + web | #208 |
| Baseline operational dashboards | Grafana boards for API + worker health | #209 |
| Outbound-dependency circuit-breaker telemetry | Breaker state metric + alert | Future issue |
| Cluster lifecycle central manager | Transitions emit through one authoritative path | #210 |

Each gap represents real work, not a lower standard. New surfaces must
satisfy their section of this model from day one — a new institution-web
PR cannot defer its §5 instrumentation to "a later PR" without an
explicit reason approved in review.

---

## 11. How this document changes

- Adding a new signal to an existing surface: add it here first, then
  add the constant / metric / SLO entry.
- Adding a new surface (e.g. Hali-ops web): add a new top-level section
  modeled on §5.
- Deprecating a signal: mark it deprecated here and keep emitting for
  one release cycle before removing.

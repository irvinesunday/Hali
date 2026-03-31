## Version: 1.0
## Last updated: 2026-03-30

# Session 07 — Notifications + Polish (Phase 12)
# Prerequisite: Session 06 complete and committed.

## Context
All civic logic is complete. The full loop from signal creation to resolution works.

## Your task this session
Build Phase 12 and complete final integration polish.

---
---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---


### Phase 12 — Notifications

#### Endpoints
- POST /v1/devices/push-token (register Expo push token, write to devices.expo_push_token)
- PUT /v1/users/me/notification-settings (opt-in preferences)
- GET /v1/users/me (account summary)

#### Push delivery (backend)
- IPushNotificationService interface
- Expo Push API implementation
- Reads expo_push_token from devices table
- Uses dedupe_key on notifications table to prevent duplicate sends
- Notification worker reads WHERE status='queued' AND send_after<=NOW()
  (ix_notifications_queued_send_after partial index covers this query)

#### Notification types to implement
- cluster_activated: notify followers when a cluster in their ward goes active
- restoration_prompt: notify previously-affected users when possible_restoration begins
- cluster_resolved: notify followers when their ward cluster resolves

#### Ward following
- GET /v1/localities/followed
- PUT /v1/localities/followed — enforce max 5 wards per account
  Reject with HTTP 422 / code: max_followed_localities_exceeded if over limit

#### GET /v1/home
Finalise the home feed response:
- activeNow: clusters in active state for followed wards
- officialUpdates: published official posts for followed wards
- recurringAtThisTime: clusters with temporal_type='recurring' matching current time slot
- otherActiveSignals: remaining active clusters

---

### Observability wiring (Item 19 — must be verified before session ends)

The `otel-collector` service is declared in docker-compose but observability
must be actively verified, not assumed. Before marking SESSION_07_COMPLETE:

#### Health endpoint (required)
Implement `GET /health` returning:
```json
{
  "status": "healthy",
  "database": "connected",
  "redis": "connected",
  "version": "1.0.0",
  "timestamp": "2026-04-01T12:00:00Z"
}
```
Return HTTP 200 when healthy, HTTP 503 when any dependency is down.
CI and the deploy workflow both call this endpoint.

#### Structured logging (verify, not just declare)
Every significant operation must emit a structured log with at minimum:
- `eventName`: the business event (e.g. `signal.submitted`, `cluster.activated`)
- `correlationId`: request-scoped trace ID
- `category`: the civic_category if applicable
- `durationMs`: elapsed time for any external call (NLP, geocoding, push)

Run the API locally and verify these fields appear in the log output.

#### SENTRY_DSN wiring (if configured)
If `SENTRY_DSN` is set in the environment, exceptions must be captured.
Test by throwing a deliberate exception in a non-critical code path and
confirming it appears in Sentry.

#### Background job logging (verify)
Every background job tick must emit:
- Job start log: `{"job": "DecayActiveClustersJob", "event": "start"}`
- Job complete log with count of clusters processed
- Job error log with full exception if the job fails

#### OpenTelemetry traces (if OTEL_EXPORTER_OTLP_ENDPOINT is set)
The API should emit traces to the OTel collector for:
- Every HTTP request (auto-instrumented via AspNetCore instrumentation)
- Every EF Core query (auto-instrumented)
- Every external API call (NLP, Geocoding, Expo Push) — manual spans

Verify by starting docker-compose and querying `localhost:4317`.

---

### Final integration polish

Run the full vertical slice again end-to-end with notifications enabled:
1. Register push token
2. Follow a ward
3. Submit signal → cluster activates → push notification fires
4. Participate as affected
5. Institution posts restoration claim → possible_restoration
6. Restoration prompt push fires to affected users
7. Vote restored → cluster resolves → resolved push fires

Fix any integration issues found during this run.

Run `dotnet test` across all test projects. Zero failures required.

## Done when
- All notification types deliver via Expo Push API
- Ward following enforces max-5 rule
- GET /v1/home returns correct 4-section response
- GET /health returns 200 with database and redis status
- Structured logs emit correlationId and eventName on every request
- Full vertical slice with notifications passes
- `dotnet test` = 0 failures
- Output: SESSION_07_COMPLETE — MVP BUILD DONE

# Hali — Worker Pipelines Implementation Guide
**All background workers, queue consumers, scheduled jobs, and event flows.**

---

## Fundamental rules for all workers

1. **Every worker is replay-safe.** The same event processed twice must produce the same result.
2. **Use the outbox.** Workers consume from `outbox_events` (or a Redis queue bridge), never from direct DB polling.
3. **Dead-letter queue.** After max retries, messages go to DLQ and raise an alert. Never silently drop.
4. **Idempotency keys.** Check before processing; write checkpoint after; never re-process a committed checkpoint.
5. **Structured logs on every meaningful action.** Include `correlationId`, `entityId`, `entityType`, `workerName`.

---

## Worker roster

| Worker | Trigger | Produces |
|---|---|---|
| `NlpExtractWorker` | `signal.submitted` | enriches signal_events with NLP output |
| `SimilarityMatchWorker` | `signal.submitted` | cluster join or create recommendation |
| `CivisAssessWorker` | `cluster.participation_recorded`, `cluster.created` | `civis_decisions` row, triggers lifecycle |
| `LifecycleTransitionWorker` | CIVIS assessment, restoration events | cluster state transitions |
| `DecayTickWorker` | Cron every 5 minutes | evaluates active clusters for decay |
| `RestorationEvaluationWorker` | Cron every 5 minutes | evaluates possible_restoration clusters |
| `NotificationWorker` | `notification.requested` | push/email/SMS sends |
| `OfficialPostProjectionWorker` | `official_post.published` | materializes home feed advisory views |
| `AnalyticsRollupWorker` | Hourly cron | metric_snapshots |

---

## Outbox bridge

Workers consume from Redis queues, not directly from `outbox_events`. A relay process moves unpublished outbox rows to Redis.

```
OutboxRelayWorker — runs every 1 second:
  SELECT * FROM outbox_events WHERE published_at IS NULL ORDER BY occurred_at LIMIT 50
  For each row:
    1. Push to Redis queue keyed by event_type
    2. UPDATE outbox_events SET published_at = now() WHERE id = {id}
    3. If Redis push fails: leave published_at null — retry next cycle
```

Queue key mapping:
```
signal.submitted                  → queue:ingestion-normalize
cluster.participation_recorded    → queue:civis-scoring
cluster.created                   → queue:civis-scoring
cluster.state_changed             → queue:notifications (conditional on state)
official_post.published           → queue:official-post-projection
signal.nlp_enriched               → queue:cluster-linking
notification.requested            → queue:notifications
```

---

## NlpExtractWorker

Consumes: `queue:ingestion-normalize`

```
Process:
  1. Idempotency check: idem:nlp:{signalEventId}
  2. Fetch signal_events row
  3. Call INlpExtractionService.ExtractAsync with text, coordinates, locale, timestamp
  4. Validate extraction against canonical taxonomy
     - If invalid: emit signal.nlp_validation_failed, log warning, return
  5. Enrich signal_events row:
     - category, subcategory_slug, condition_slug, neutral_summary, temporal_type
     - Write nlp_confidence + location_confidence into civis_precheck JSONB
  6. Write idempotency checkpoint
  7. Emit outbox: signal.nlp_enriched → queue:cluster-linking
```

---

## CivisAssessWorker

Consumes: `queue:civis-scoring`

```
Process:
  1. Idempotency check: idem:civis:{correlationId}
  2. Call CivisEngine.EvaluateActivation(clusterId)
     - This internally persists CivisDecision row
  3. Emit outbox: civis.assessment_completed → queue:cluster-lifecycle
  4. Write idempotency checkpoint
```

---

## LifecycleTransitionWorker

Consumes: `queue:cluster-lifecycle`

```
Process:
  1. Idempotency check: idem:lifecycle:{correlationId}
  2. Call CivisEngine.HandleClusterTransitionAsync(clusterId)
     - State change is transactional with DB trigger (trigger emits outbox event)
  3. Write idempotency checkpoint
```

---

## DecayTickWorker

Runs every 5 minutes (cron — not event-driven).

```
Process:
  1. Query: SELECT id FROM signal_clusters WHERE state IN ('active') ORDER BY last_seen_at
  2. For each cluster: call CivisEngine.ShouldDecayToResolved(cluster)
  3. If should decay: call HandleClusterTransitionAsync(clusterId)
  4. Log: DecayTickWorker evaluated {count} clusters, transitioned {n}
```

---

## RestorationEvaluationWorker

Runs every 5 minutes (cron).

```
Process:
  1. Query: SELECT id FROM signal_clusters WHERE state = 'possible_restoration'
  2. For each: call CivisEngine.EvaluateRestoration(clusterId)
  3. Apply transition if confirmed or rejected
  4. If timeout (24h in possible_restoration with no resolution): revert to active
```

---

## NotificationWorker

Consumes: `queue:notifications`

```
Process:
  1. Check dedupe_key: if notification row already exists with that key, skip
  2. Fetch notification row — if status != 'queued', skip
  3. Check quiet hours (06:00–22:00 local time for non-urgent types)
     - If outside window: update send_after to next window, leave in queue
  4. Dispatch by channel:
     push  → Expo Push API
     email → email provider
     sms   → Africa's Talking
  5. Update notification.status = 'sent' or 'failed', set sent_at
```

Notification types and triggers:

| Type | Trigger | Recipients |
|---|---|---|
| `cluster_activated_in_followed_ward` | cluster → active | citizens following that locality |
| `restoration_prompt` | cluster → possible_restoration | previously `affected` citizens |
| `cluster_resolved` | cluster → resolved | previously `affected` citizens |
| `cluster_activated_in_scope` *(Phase 2)* | cluster → active | institution notification recipients |

---

## Scheduled jobs

```
DecayTickWorker              every 5 min
RestorationEvalWorker        every 5 min
OfficialPostExpiryWorker     every 10 min  — sets status=expired where ends_at < now()
LocalitySnapshotCacheJob     every 2 min   — refreshes Redis cache for hot localities
AnalyticsRollupWorker        hourly        — writes metric_snapshots rows
OutboxRelayWorker            every 1 sec   — bridges DB outbox to Redis queues
```

---

## Retry policy

```
Idempotent jobs (most workers):
  Max attempts: 8
  Backoff: exponential with jitter, base delay 5 seconds

Non-idempotent side-effect jobs (notification sends):
  Max attempts: 3
  Delays: 30s, 2min, 5min

After max retries:
  Move to DLQ: queue:{workerName}:dlq
  Write admin_audit_log entry: action=worker.dlq_message
  Raise structured alert
```

---

## Event envelope (all outbox payloads)

```json
{
  "eventId": "uuid",
  "eventName": "cluster.state_changed",
  "occurredAtUtc": "2026-04-03T14:30:00Z",
  "entityType": "signal_cluster",
  "entityId": "uuid",
  "correlationId": "uuid",
  "causationId": "uuid",
  "schemaVersion": "1.0",
  "payload": { }
}
```

All workers must propagate `correlationId` through the full chain. The originating API request sets the initial `correlationId` and passes it via headers into outbox payloads.

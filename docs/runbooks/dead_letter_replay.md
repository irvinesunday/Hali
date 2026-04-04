# Runbook: Dead Letter Queue Non-Empty

## Severity
P2

## Symptom
Messages have landed in the dead-letter queue after exhausting retry attempts. These represent events that failed processing and need manual investigation and replay.

## Immediate Triage
1. Count DLQ messages: check outbox_events with repeated failures or a dedicated DLQ table
2. Inspect the failed event payloads for common patterns (bad data, schema mismatch, etc.)
3. Check worker logs around the time events were dead-lettered for root cause

## Resolution Steps
1. Identify root cause: bad payload, downstream service failure, or bug in processing logic
2. If bad payload: fix the data and replay; discard truly invalid events
3. If transient failure (service was down): replay all DLQ messages after service recovery
4. Replay command: update `published_at = NULL` on affected outbox rows to re-trigger processing
5. Monitor that replayed events process successfully

## Escalation
- P2: Notify on-call engineer via Slack
- If DLQ depth exceeds 100: escalate to engineering lead

## Related Alerts
- `nlp_worker_lag_high`
- `civis_worker_lag_high`

## Post-Incident
- File incident report
- Add validation or error handling to prevent recurrence

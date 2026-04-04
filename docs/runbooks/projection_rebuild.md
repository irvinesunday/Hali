# Runbook: Projection Rebuild Mismatch

## Severity
P3

## Symptom
Event replay produced an inconsistent cluster projection. The rebuilt cluster state does not match the current live state, indicating potential event loss or processing bugs.

## Immediate Triage
1. Identify the affected cluster(s) from the mismatch metric labels
2. Compare the replayed state vs live state: check `signal_clusters` table for the cluster
3. Check outbox_events for any missing or duplicate events for the affected aggregate_id
4. Check if any recent deployments changed event processing logic

## Resolution Steps
1. If missing events: investigate why events were lost (DLQ, worker crash during processing)
2. If duplicate events: check idempotency guards in the event handler
3. If processing logic changed: determine which version is correct (old or new)
4. To fix the cluster state:
   - Option A: Replay all events for the cluster to rebuild its projection
   - Option B: Manually correct the cluster state if replay is not possible
5. Verify affected participation counts and CIVIS scores are consistent

## Escalation
- P3: Create GitHub issue, assign to next sprint
- If multiple clusters affected: escalate to P2

## Related Alerts
- `dead_letter_queue_non_empty`

## Post-Incident
- File incident report
- Add automated projection consistency check to CI or periodic job

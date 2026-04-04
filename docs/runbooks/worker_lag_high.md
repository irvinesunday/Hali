# Runbook: Worker Queue Lag High

## Severity
P2

## Symptom
NLP ingestion queue depth above 50 or CIVIS scoring queue depth above 100 for more than 10 minutes. Signal processing is delayed — users may not see their submitted signals reflected in clusters promptly.

## Immediate Triage
1. Check queue depths: inspect outbox_events table for unpublished rows: `SELECT COUNT(*) FROM outbox_events WHERE published_at IS NULL;`
2. Check worker logs for errors or restarts
3. Verify worker process is running: check container/pod status
4. Check if external dependency (Anthropic API for NLP) is slow or down

## Resolution Steps
1. If worker process crashed: restart it
2. If Anthropic API is slow: check their status page; consider increasing worker timeout
3. If burst of traffic caused backlog: workers will catch up naturally — monitor depth trending down
4. If rows older than 60 seconds exist in outbox: check for poison messages blocking the relay
5. If persistent: scale worker instances horizontally

## Escalation
- P2: Notify on-call engineer via Slack within 30 minutes
- If queue depth continues growing after 30 minutes: escalate to P1

## Related Alerts
- `dead_letter_queue_non_empty`
- `api_error_rate_high`

## Post-Incident
- File incident report
- Assess whether worker scaling policy needs adjustment

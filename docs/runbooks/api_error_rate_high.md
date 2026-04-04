# Runbook: API Error Rate High

## Severity
P1

## Symptom
API 5xx error rate exceeds 5% over a 5-minute window. Users may experience failed requests across all endpoints.

## Immediate Triage
1. Check Grafana dashboard for error rate breakdown by endpoint and status code
2. Verify `/health` endpoint: `curl -s http://api:8080/health | jq .`
3. Determine scope: single instance or all instances? Check each pod/container.
4. Check Sentry for new error groups correlating with the spike

## Resolution Steps
1. If database-related: check `database_connection_failure` runbook
2. If Redis-related: check `redis_connection_failure` runbook
3. If a single endpoint is responsible: check recent deployments for breaking changes
4. If OOM or resource exhaustion: scale horizontally or increase resource limits
5. If external dependency (Anthropic, Africa's Talking, Nominatim): check their status pages and consider circuit-breaking

## Escalation
- P1: Page on-call engineer immediately via PagerDuty
- If not resolved within 15 minutes: escalate to engineering lead

## Related Alerts
- `database_connection_failure`
- `redis_connection_failure`
- `otp_delivery_failure`

## Post-Incident
- File incident report within 24 hours
- Add regression test if applicable
- Update SLO error budget tracking

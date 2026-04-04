# Runbook: Redis Connection Failure

## Severity
P1

## Symptom
API cannot reach Redis. The `/health` endpoint reports `redis: unhealthy`. Rate limiting, OTP storage, home feed caching, and background job queues are degraded.

## Immediate Triage
1. Check `/health` response: `curl -s http://api:8080/health | jq .redis`
2. Verify Redis is running: `redis-cli -h <redis-host> ping`
3. Check Redis memory usage: `redis-cli info memory`
4. Check for OOM-killer activity in system logs

## Resolution Steps
1. If Redis process is down: restart the service or container
2. If OOM: increase `maxmemory` setting or evict stale keys (`redis-cli DBSIZE` to assess)
3. If network partition: verify connectivity between API and Redis
4. If persistent: check if Redis is configured with AOF/RDB and disk is full

## Escalation
- P1: Page on-call engineer immediately via PagerDuty
- Redis data is ephemeral (cache + rate limits) — data loss is acceptable but service must recover

## Related Alerts
- `api_error_rate_high`

## Post-Incident
- File incident report
- Verify rate limiting and caching resume correctly after recovery

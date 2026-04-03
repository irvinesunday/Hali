# Runbook: Database Connection Failure

## Severity
P1

## Symptom
API cannot reach PostgreSQL. The `/health` endpoint reports `database: unhealthy`. All write operations and most reads will fail.

## Immediate Triage
1. Check `/health` response: `curl -s http://api:8080/health | jq .database`
2. Verify PostgreSQL is running: `pg_isready -h <db-host> -p 5432`
3. Check connection pool exhaustion in application logs (search for `NpgsqlException`)
4. Check disk space on the database server: `df -h`
5. Check PostgreSQL logs for crash/restart indicators

## Resolution Steps
1. If PostgreSQL process is down: restart the service or container
2. If connection pool exhausted: restart API pods to reset connections; investigate long-running queries
3. If disk full: clear WAL archives or expand storage; run `VACUUM` if bloat is the cause
4. If network partition: verify security group / firewall rules between API and DB
5. If migration failure: see `docs/runbooks/migration-rollback.md`

## Escalation
- P1: Page on-call engineer immediately via PagerDuty
- If data loss suspected: escalate to engineering lead and begin backup restoration assessment

## Related Alerts
- `api_error_rate_high`

## Post-Incident
- File incident report
- Verify all migrations are consistent post-recovery
- Check for data integrity issues

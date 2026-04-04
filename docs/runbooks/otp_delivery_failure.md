# Runbook: OTP Delivery Failure

## Severity
P2

## Symptom
SMS OTP delivery failure rate exceeds 10% over 10 minutes. Users cannot authenticate or register — this blocks the entire auth flow.

## Immediate Triage
1. Check Africa's Talking API status: https://status.africastalking.com
2. Check application logs for `ISmsProvider` error patterns
3. Verify Africa's Talking credentials: `AFRICASTALKING_API_KEY` and `AFRICASTALKING_USERNAME`
4. Check if failures are concentrated on specific phone number prefixes (carrier-specific issue)

## Resolution Steps
1. If Africa's Talking is down: wait for recovery; consider enabling a fallback SMS provider if available
2. If credentials expired or revoked: rotate credentials and restart API
3. If rate-limited by Africa's Talking: check account balance and usage limits
4. If carrier-specific: document affected prefixes and report to Africa's Talking support

## Escalation
- P2: Notify on-call engineer via Slack within 30 minutes
- If prolonged (>30 minutes): escalate to P1 — users cannot log in

## Related Alerts
- `api_error_rate_high`

## Post-Incident
- File incident report
- Assess whether a secondary SMS provider should be integrated for failover

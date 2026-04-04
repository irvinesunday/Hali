# Runbook: Notification Delivery Degraded

## Severity
P2

## Symptom
Push notification delivery success rate has dropped below 90% over 15 minutes. Users are not receiving cluster activation, restoration prompts, or resolution notifications.

## Immediate Triage
1. Check Expo Push API status: https://status.expo.dev
2. Check `SendPushNotificationsJob` worker logs for error patterns
3. Verify Expo access token is valid and not expired
4. Check if delivery failures are concentrated on specific device types (iOS vs Android)

## Resolution Steps
1. If Expo API is down: wait for recovery — notifications will be retried on next worker cycle
2. If token expired: rotate `EXPO_ACCESS_TOKEN` in environment configuration and restart workers
3. If high rate of `DeviceNotRegistered` errors: stale push tokens — schedule a token cleanup job
4. If specific error codes from Expo: consult Expo Push API error reference

## Escalation
- P2: Notify on-call engineer via Slack within 30 minutes
- If prolonged (>1 hour): notify product team — users are not receiving time-sensitive civic alerts

## Related Alerts
- `api_error_rate_high`

## Post-Incident
- File incident report
- Consider adding push token validation on device registration

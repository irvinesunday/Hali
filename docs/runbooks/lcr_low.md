# Runbook: Loop Closure Rate Low

## Severity
P4

## Symptom
Loop closure rate (LCR) has dropped below 25% over 7 days. This means fewer than 25% of active clusters are reaching the resolved state within 72 hours. This is the core Hali KPI.

## Immediate Triage
1. Check which categories have the lowest closure rates
2. Check if institutions are actively responding to clusters in their jurisdictions
3. Check if restoration prompts are being sent and received (notification delivery)
4. Check if CIVIS decay thresholds are resolving clusters too aggressively (masking real unresolved issues)

## Resolution Steps
1. If institutions are not responding:
   - This is a product/engagement issue, not a technical one
   - Notify product team to review institution engagement strategy
2. If restoration prompts are not being sent:
   - Check `SendPushNotificationsJob` worker for errors
   - Check `notification_delivery_rate_low` alert
3. If decay is resolving clusters prematurely:
   - Review `CIVIS_DEACTIVATION_THRESHOLD` — it may be too aggressive
   - Consider per-category tuning of half-life values
4. If users are not responding to restoration prompts:
   - Product team should review prompt timing and UX

## Escalation
- P4: Create GitHub issue, assign to next sprint
- Share weekly LCR report with product team

## Related Alerts
- `cluster_activation_rate_low`
- `notification_delivery_rate_low`

## Post-Incident
- Track LCR trend weekly
- Adjust CIVIS constants if the pattern is systemic

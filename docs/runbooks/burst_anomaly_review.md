# Runbook: Burst Anomaly Detected

## Severity
P3

## Symptom
Sudden burst of same-category signals from low-diversity devices concentrated in a narrow H3 geo-cell. This may indicate coordinated inauthentic activity (signal stuffing) or a genuine large-scale event.

## Immediate Triage
1. Query the burst details: identify the H3 cell, category, time window, and device fingerprints involved
2. Check device diversity: are multiple signals coming from the same or very similar device fingerprints?
3. Check if there's a real-world event that explains the burst (news, social media corroboration)
4. Check CIVIS activation state of affected clusters

## Resolution Steps
1. If legitimate burst (real event): no action needed — CIVIS activation will handle it normally
2. If suspected inauthentic activity:
   - Block offending device fingerprints via the admin API
   - Review and potentially deactivate the resulting cluster if it was improperly activated
   - Do NOT delete citizen signal events — preserve for audit
3. Document the incident and any devices blocked

## Escalation
- P3: Create GitHub issue, assign to next sprint
- If large-scale coordinated attack: escalate to P2 and notify engineering lead

## Related Alerts
- `cluster_activation_rate_low` (may spike falsely during burst)

## Post-Incident
- File incident report
- Review CIVIS device diversity thresholds
- Consider tightening per-device rate limits for the affected category

# Runbook: Out-of-Jurisdiction Post Attempt

## Severity
P3

## Symptom
An institution account attempted to publish an official post targeting localities outside its assigned jurisdiction. The API returned 403 and blocked the post.

## Immediate Triage
1. Identify the institution and account from the audit log
2. Check the institution's jurisdiction assignment in `institution_jurisdictions` table
3. Verify whether the post's target scopes actually overlap with the institution's jurisdiction
4. Determine if this is a misconfiguration or a deliberate attempt

## Resolution Steps
1. If misconfiguration (institution's jurisdiction is too narrow):
   - Admin updates the institution's jurisdiction via the admin API
   - Notify the institution representative that they can retry
2. If deliberate out-of-scope attempt:
   - Document the incident
   - No immediate action needed — the API already blocked the post
   - Consider if the institution account should be reviewed
3. If a bug in jurisdiction check geometry:
   - File a bug report with the specific locality IDs and institution ID
   - Check PostGIS intersection query for edge cases

## Escalation
- P3: Create GitHub issue, assign to next sprint
- If repeated attempts from same institution: notify admin for review

## Related Alerts
- None directly related

## Post-Incident
- File incident report if pattern indicates deliberate misuse
- Review jurisdiction boundary data for accuracy

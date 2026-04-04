# Runbook: Cluster Activation Rate Low

## Severity
P4

## Symptom
Cluster activation rate has dropped below 20% over 24 hours. Fewer than 20% of submitted signals are resulting in activated clusters. Users may perceive Hali as unresponsive — their reports don't appear to "do anything."

## Immediate Triage
1. Check if signal submission volume has changed (low volume naturally lowers activation rate)
2. Check CIVIS `min_unique_devices_for_activation` — is it too high for current user density?
3. Check if signals are being clustered but not reaching activation threshold
4. Check per-category activation rates — some categories may be more affected than others

## Resolution Steps
1. If low user density (MVP launch):
   - Consider temporarily lowering `min_unique_devices_for_activation` from 2 to 1 for specific categories
   - This is a product decision — consult with product team
2. If clustering is not working:
   - Check H3 resolution is correctly matching nearby signals
   - Check join_threshold (0.65) — may be too strict for the local signal patterns
3. If specific category is affected:
   - Review MACF thresholds for that category
   - Check if half_life_hours is appropriate

## Escalation
- P4: Create GitHub issue, assign to next sprint
- Share activation rate breakdown by category with product team

## Related Alerts
- `loop_closure_rate_low`

## Post-Incident
- Track activation rate trend daily during launch period
- Adjust CIVIS per-category constants based on observed patterns

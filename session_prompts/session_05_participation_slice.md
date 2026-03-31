## Version: 1.0
## Last updated: 2026-03-30

# Session 05 — Participation + Vertical Slice Test (Phase 9)
# Prerequisite: Session 04 complete and committed.

## Context
Clustering and CIVIS are complete. Clusters activate and decay correctly.

## Your task this session
Build Phase 9, then run the vertical slice test before this session ends.

---
---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---


### Phase 9 — Participation

#### Endpoints
- POST /v1/clusters/{id}/participation
- POST /v1/clusters/{id}/context
- POST /v1/clusters/{id}/restoration-response

#### Business rules (from claude.md Application-Layer Business Rules)

**One participation type per device per cluster:**
Before inserting, delete or soft-replace any existing row for (cluster_id, device_id).
The DB unique constraint alone does not enforce this.

**context only after affected:**
POST /v1/clusters/{id}/context must check that the requesting device has
an active 'affected' participation on that cluster.
Reject with HTTP 422 / code: context_requires_affected_participation if not.

**Context edit window:**
Only accept context edits within CIVIS_CONTEXT_EDIT_WINDOW_MINUTES (2) of the
original affected participation created_at.
Reject with HTTP 422 / code: context_edit_window_expired after the window.

**Restoration response:**
POST /v1/clusters/{id}/restoration-response accepts: still_affected | restored | not_sure
Maps to participation_type: affected | restoration_yes | restoration_unsure
Triggers restoration evaluation in clustering worker.

#### Counts maintenance
After every participation write, update signal_clusters.affected_count
and signal_clusters.observing_count to reflect current totals.

#### Tests required
- One-type-per-device enforcement (second participation type replaces first)
- Context gate (rejected without prior affected participation)
- Edit window enforcement (rejected after CIVIS_CONTEXT_EDIT_WINDOW_MINUTES)
- Restoration response records correctly and triggers evaluation

---

### Vertical Slice Test (mandatory before session ends)

Run this end-to-end flow manually or via integration test:

1. POST /v1/auth/otp → POST /v1/auth/verify (get token)
2. POST /v1/signals/preview (submit free text: "Big potholes near National Oil, Nairobi West")
3. Confirm extraction returns category=roads, subcategory=potholes
4. POST /v1/signals/submit (confirm and submit)
5. Verify signal_event row created with lat/lng, location_confidence, condition_confidence
6. Verify signal_cluster row created with state=unconfirmed
7. Submit 2 more signals from different device fingerprints
8. Verify CIVIS promotes cluster to state=active
9. POST /v1/clusters/{id}/participation with type=affected
10. POST /v1/clusters/{id}/context with text
11. Verify context rejected after 2 minutes
12. POST /v1/clusters/{id}/restoration-response with restored
13. GET /v1/clusters/{id} — verify state reflects possible_restoration

All 13 steps must pass before SESSION_05_COMPLETE.

## Done when
- Participation endpoints enforce all business rules
- Vertical slice passes all 13 steps
- Output: SESSION_05_COMPLETE

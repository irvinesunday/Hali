## Version: 1.0
## Last updated: 2026-03-30

# Session 06 — Official Updates + Restoration (Phases 10 + 11)
# Prerequisite: Session 05 complete and vertical slice passing.

## Context
The core civic loop works end-to-end. Participation is enforced.

## Your task this session
Build Phases 10 and 11. Do not begin Phase 12 (Notifications).

---
---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---


### Phase 10 — Official updates

#### Endpoints
- POST /v1/official-posts (institution creates update)
- GET /v1/clusters/{id} — must return official posts alongside citizen signal
- GET /v1/home — must include officialUpdates section

#### Institution geo-scope enforcement
When an institution creates a post, validate that the post scope
(official_post_scopes.geom) intersects the institution's jurisdiction
(institution_jurisdictions.geom).
Reject with HTTP 403 / code: outside_jurisdiction if not.
Use ST_Intersects for the check. The GiST index on both columns makes this fast.

#### Dual visibility doctrine
Official posts and citizen clusters are ALWAYS returned side by side.
Neither suppresses the other. An institution claiming restoration does NOT
auto-resolve a cluster — it triggers possible_restoration state only.

#### Official post types
- live_update: informational, no state effect
- scheduled_disruption: future-dated, appears in Official Updates section
- advisory_public_notice: informational advisory

#### Official update expiry
ExpireOfficialPostsJob (every 10 min) — set status='expired' on posts where
ends_at is not null and ends_at < NOW().

---

### Phase 11 — Restoration

#### Restoration trigger sources
1. Institution posts a live_update with is_restoration_claim=true
   → cluster transitions to possible_restoration
2. Citizen taps "No longer affected" (restoration_yes participation)
   → if enough votes accumulate, EvaluatePossibleRestorationJob resolves

#### Restoration resolution formula (from mvp_locked_decisions.md §9)
restoration_ratio = affected_restore_votes / total_affected_votes
Resolve if: restoration_ratio >= CIVIS_RESTORATION_RATIO (0.60)
            AND affected_restore_votes >= CIVIS_MIN_RESTORATION_AFFECTED_VOTES (2)

#### EvaluatePossibleRestorationJob (every 5 min)
For clusters in possible_restoration state:
- Count restoration_yes vs still-affected votes in affected cohort
- If threshold met → transition to resolved, write civis_decisions record
- If still-affected votes return → revert to active

#### Tests required
- Institution post outside jurisdiction rejected
- live_update + is_restoration_claim triggers possible_restoration
- Restoration ratio calculation at exactly 60%, below, above
- Revert to active when still-affected votes counter restoration

## Done when
- GET /v1/home returns officialUpdates section
- GET /v1/clusters/{id} shows official posts in parallel
- Restoration transitions work correctly
- Output: SESSION_06_COMPLETE

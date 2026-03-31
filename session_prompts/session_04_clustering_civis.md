## Version: 1.0
## Last updated: 2026-03-30

# Session 04 — Clustering + CIVIS (Phases 7 + 8)
# Prerequisite: Session 03 complete and committed.
# WARNING: This is the most complex session. Take it carefully.

## Context
Signal ingestion, NLP, and geocoding are complete. Signals are persisting correctly.

## Your task this session
Build Phases 7 and 8. Do not begin Phase 9 (Participation).

---
---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---


### Phase 7 — Clustering

Read these first: mvp_locked_decisions.md §5 (Spatial), §9 CIVIS Formulas (join_score).

#### Join-score algorithm
join_score = 0.40 * category_match + 0.25 * distance_score + 0.20 * time_score + 0.15 * condition_match
Join existing cluster if join_score >= CIVIS_JOIN_THRESHOLD (0.65)

#### Candidate search
1. Compute H3 cell (resolution 9) from signal lat/lng using H3.net
2. Search signal_clusters where spatial_cell_id IN (cell + k-ring-1 neighbors)
   AND category = signal category AND state IN ('unconfirmed', 'active')
3. Score each candidate using join_score formula
4. If best score >= threshold → attach to cluster (insert cluster_event_links row)
5. If no match → create new signal_cluster with state = 'unconfirmed'
6. Emit outbox event on every cluster create or update

#### Tables written
- signal_clusters (create or update last_seen_at, raw_confirmation_count)
- cluster_event_links (link signal_event to cluster)
- outbox_events (cluster.created or cluster.updated event)

---

### Phase 8 — CIVIS logic

Read mvp_locked_decisions.md §9 fully. All constants come from configuration.

#### WRAB computation
Window: CIVIS_WRAB_ROLLING_WINDOW_DAYS (default 30)
WRAB = Σ weighted_active_signal_events in window
effective_WRAB = max(WRAB, base_floor[category])

#### SDS computation
active_mass_now = sum of recent signal event weights in evaluation horizon
SDS = active_mass_now / effective_WRAB

#### MACF computation
MACF = clamp(ceil(base_floor + log2(SDS + 1)), macf_min, macf_max)
All per-category values from environment (CIVIS_ROADS_BASE_FLOOR etc.)

#### Activation gate
Cluster becomes ACTIVE when:
- raw_confirmation_count >= MACF
- COUNT(DISTINCT device_id) in cluster_event_links >= CIVIS_MIN_UNIQUE_DEVICES (2)

#### Decay worker (DecayActiveClustersJob — every 5 min)
- Apply exponential decay per category half_life_hours
- Lambda = ln(2) / half_life_hours
- If live_mass falls below deactivation threshold → transition to resolved or expired

#### State transitions
Write every state change to civis_decisions table with reason_codes.
Emit cluster_state_changed outbox event on every transition.

#### Tests required (mandatory for CIVIS)
- MACF calculation at SDS=0, SDS=1, SDS=5 for each category
- Activation gate pass and fail cases
- Device diversity gate (same device repeated = still only 1)
- Decay calculation at t=0, t=half_life, t=2*half_life
- State transition: unconfirmed → active → possible_restoration → resolved

## Done when
- Clustering routes a new signal to correct cluster or creates a new one
- CIVIS activation promotes an unconfirmed cluster to active on threshold
- Decay worker runs and transitions clusters correctly
- All CIVIS unit tests pass
- Output: SESSION_04_COMPLETE

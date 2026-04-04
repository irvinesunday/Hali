# Session Prompt Patch Notes
**Read this before running any session. Documents all changes from the Platform Reconciliation that affect session prompt content.**

These are not changes to the session prompt files themselves — they are overrides.
When a session prompt conflicts with a point below, this document wins.

---

## Affects: Session 00 (sync patch)

### OpenAPI patches required (supersedes session_00 patch list)

Session 00 must apply ALL of the following — the original patch list was incomplete:

1. Rename `/v1/auth/request-otp` → `/v1/auth/otp`
2. Rename `/v1/auth/verify-otp` → `/v1/auth/verify`
3. Add `POST /v1/auth/refresh`
4. Add `POST /v1/auth/logout`
5. Rename `POST /v1/signals` → `POST /v1/signals/submit`
6. Add `GET /v1/home`
7. Add `POST /v1/clusters/{id}/context`
8. Add `POST /v1/clusters/{id}/restoration-response`
9. Add `PUT /v1/localities/followed`
10. Add `POST /v1/devices/push-token`
11. Add `PUT /v1/users/me/notification-settings`
12. Add `GET /v1/users/me`
13. Fix `civic_category` enum to exactly 8 values: roads, transport, electricity, water, environment, safety, governance, infrastructure
14. Add explicit enum constraint to ALL `category` fields (currently `type: string` with no enum)
15. Add pagination query parameters (`cursor`, `limit`) to all list endpoints
16. Remove `/v1/admin/*` routes — deprecated, replaced in Phase 2 by `/v1/ops/*`

### Schema: DO NOT replicate these constraints from 01_postgres_schema.sql

The original file has two broken constraints. Use the corrected versions from docs/arch/01_schema_reference.md:

```
WRONG  → unique(locality_id, category, spatial_cell_id, state, title)  on signal_clusters
CORRECT → partial unique index on (locality_id, category, spatial_cell_id)
          WHERE state IN ('unconfirmed', 'active', 'possible_restoration')

WRONG  → unique(cluster_id, device_id, participation_type, idempotency_key)  on participations
CORRECT → two separate partial indexes:
          ix_participations_device_cluster_type ON (cluster_id, device_id, participation_type) WHERE idempotency_key IS NOT NULL
          ix_participations_idempotency ON (idempotency_key) WHERE idempotency_key IS NOT NULL
```

### New tables required in Session 00 migrations (add to migration scaffold)

- `refresh_tokens` — CRITICAL for Phase 1 auth (see docs/arch/01_schema_reference.md §refresh_tokens)
- `official_posts.is_restoration_claim` boolean column — required for lifecycle worker
- `tda_snapshots` — add to Phase 1 migration set
- Outbox trigger: `fn_emit_cluster_state_changed` on signal_clusters UPDATE

### seed_taxonomy_expanded.sql — TABLE NAME BUG

The file inserts into `civic_subcategories` which does not exist.
Correct target: `taxonomy_categories(category, subcategory_slug, display_name)`.
Do not run the original file. Generate correct seed SQL from docs/arch/01_schema_reference.md §taxonomy.

---

## Affects: Session 02 (auth)

### Refresh token shape (supersedes session_02 if it has an older shape)

The `refresh_tokens` table must include `replaced_by_token_hash varchar(128)`.
This column is required for token theft detection — not optional.

On refresh:
- If `replaced_by_token_hash` is set on the presented token → TOKEN THEFT → revoke entire account token family → 401 `token_family_compromised`
- If valid → generate new pair, mark old row with `revoked_at` + `replaced_by_token_hash`

### JWT audience

Set `JWT_AUDIENCE=hali-platform` (not `hali-mobile`). Three web surfaces will eventually share this issuer.

---

## Affects: Session 03 (signal intake / NLP)

### NLP extraction confidence thresholds drive Step 2 UI

The mobile Step 2 screen behaviour is determined by these thresholds (locked):

```
Location confidence:
  >= 0.80  → pre-fill, no mandatory confirm
  0.50–0.79 → show yellow "confirm location" badge — user MUST confirm or edit
  < 0.50   → show empty field with search input — user MUST fill before Step 3

Condition confidence:
  >= 0.75  → accept as suggested
  0.50–0.74 → editable confirmation
  < 0.50   → treat as weak, show empty dropdown
```

NLP must never block signal submission on failure. If extraction fails: return fallback candidate, queue for background enrichment.

---

## Affects: Session 04 (clustering / CIVIS)

### MACF formula (locked — use exactly)

```csharp
decimal rawMacf = catConfig.BaseFloor
                + catConfig.Alpha * (decimal)Math.Log2((double)(1 + sds))
                + catConfig.SensitivityUplift
                + geoUncertainty * 0.5m;

int macf = (int)Math.Ceiling(rawMacf);
return Math.Clamp(macf, catConfig.MacfMin, catConfig.MacfMax);
// Alpha = 1.0 for all categories in MVP
// SensitivityUplift = 0 for all except safety (= 1)
```

### Join score formula (locked — use exactly)

```
join_score = 0.40 × category_score
           + 0.25 × distance_score   (H3 ring decay: ring 0=1.0, 1=0.85, 2=0.65, 3=0.35, 4+=0)
           + 0.20 × time_score       (exponential decay using category half-life)
           + 0.15 × condition_score

Join if score >= 0.65
```

### Outbox trigger

The DB trigger on signal_clusters must be created in this session's migrations.
State transitions are only valid if the trigger exists — it ensures atomic outbox emission.
See docs/arch/01_schema_reference.md §trigger for the full SQL.

---

## Affects: Session 05 (participation / vertical slice)

### Context edit window enforcement

The 2-minute context edit window is measured from the created_at of the most recent `affected` participation.
The endpoint POST /v1/clusters/{id}/context must return 422 policy_blocked with code `context_window_expired` after this window.

### Offline queue requirement (mobile)

The mobile app must queue these write operations locally when offline:
- POST /v1/signals/submit
- POST /v1/clusters/{id}/participation
- POST /v1/clusters/{id}/restoration-response
- POST /v1/clusters/{id}/context

Idempotency keys are generated client-side before queueing. On reconnect, flush queue using stored keys.
Read paths (home feed, cluster detail) must serve cached last response with freshness timestamp.

### Ward max-5 enforcement

Server must return 422 policy_blocked with code `max_followed_wards_reached` when a 6th ward is attempted.
PUT /v1/localities/followed must reject payloads with more than 5 items with the same code.

### Push token registration timing

Register push token immediately after successful OTP verification — not at app launch.
If permission denied: skip silently, do not block onboarding.

---

## Affects: Session 06 (official updates / restoration)

### is_restoration_claim field

Official posts have a boolean `is_restoration_claim` column.
When `is_restoration_claim = true` AND `related_cluster_id` is set:
- Emit outbox event `official_post.restoration_claim`
- lifecycle_transition_worker handles possible_restoration state transition
- Do NOT auto-resolve. Citizens still need to confirm.
- is_restoration_claim posts are immutable after publish. No edit path.

### Restoration confirmation rule (locked)

```
restoration_ratio = weighted_yes_votes / (weighted_yes_votes + weighted_still_affected_votes)

Resolve if:
  restoration_ratio >= 0.60
  AND distinct_affected_devices >= 2
  AND cooldown_since_last_negative_evidence satisfied

Only count votes from accounts/devices that previously submitted affected participation
(they have ground truth — general observers do not vote in restoration)
```

---

## Affects: All sessions

### civic_category enum — exactly 8 values

Any session that references the civic_category enum must use exactly these 8 values:
roads, transport, electricity, water, environment, safety, governance, infrastructure

`health`, `education`, and `other` have been removed from the MVP enum.
Any migration or seed that includes them is incorrect.

### CIVIS internals — blocked in all responses

These fields must never appear in any endpoint response regardless of surface:
civis_score, wrab, sds, macf, raw_confirmation_count, civis_precheck, reason_codes (from civis_decisions)

This is a contract test requirement. Agent C will fail validation if they appear.

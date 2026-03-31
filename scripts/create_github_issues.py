#!/usr/bin/env python3
"""
Hali MVP — GitHub Issues Automation
Creates Epics (milestones), User Stories, and Tasks from the build pack.

Usage:
    export GITHUB_TOKEN=your_pat_here
    export GITHUB_REPO=irvinesunday/hali
    python3 scripts/create_github_issues.py

Requirements:
    pip install PyGithub
"""

import os
import sys
import time
from github import Github, GithubException

# ─── Configuration ────────────────────────────────────────────────────────────

GITHUB_TOKEN = os.environ.get("GITHUB_TOKEN")
GITHUB_REPO  = os.environ.get("GITHUB_REPO")  # e.g. "irvine/hali"

if not GITHUB_TOKEN or not GITHUB_REPO:
    print("ERROR: Set GITHUB_TOKEN and GITHUB_REPO environment variables.")
    sys.exit(1)

# ─── Epic → Milestone + label colour ─────────────────────────────────────────

LABEL_COLOURS = {
    "epic":              "7057ff",
    "user-story":        "0075ca",
    "task":              "e4e669",
    "backend":           "d93f0b",
    "frontend":          "0052cc",
    "infra":             "006b75",
    "civis":             "b60205",
    "nlp":               "e99695",
    "auth":              "fbca04",
    "testing":           "5319e7",
    "session-00":        "ededed",
    "session-01":        "ededed",
    "session-02":        "ededed",
    "session-03":        "ededed",
    "session-04":        "ededed",
    "session-05":        "ededed",
    "session-06":        "ededed",
    "session-07":        "ededed",
}

# ─── Full issue tree ──────────────────────────────────────────────────────────
# Structure:
#   epic:    { title, description, milestone_title, labels }
#   stories: [ { title, body, labels, tasks: [...] } ]

ISSUE_TREE = [

    # ── EPIC 0: Sync Patch ─────────────────────────────────────────────────
    {
        "epic": {
            "title": "Epic 0 — Synchronisation Patch",
            "description": "Patch OpenAPI spec, scaffold EF Core migrations, seed taxonomy, align frontend stubs. Must complete before any build phase begins.",
            "milestone": "00 — Sync Patch",
            "labels": ["epic", "infra", "session-00"],
        },
        "stories": [
            {
                "title": "Story 0.1 — Patch OpenAPI spec to canonical /v1/* routes",
                "body": """**As** the build system,
**I need** the OpenAPI spec to match the locked endpoint list in claude.md
**So that** backend controllers and mobile API calls share a single source of truth.

**Acceptance criteria:**
- All old paths removed (request-otp, verify-otp, plural participations, POST /v1/signals)
- All 17 required endpoints present with correct methods
- SignalCandidate schema includes conditionConfidence, locationSource, locationPrecisionType
- HomeFeedResponse and HomeFeedClusterItem schemas defined inline (no dangling $refs)
- POST /v1/localities/followed changed to PUT
- OpenAPI file is valid (no broken $ref)

**Ref:** openapi_patch_checklist.md Steps 1–4""",
                "labels": ["user-story", "backend", "session-00"],
                "tasks": [
                    ("Remove stale OpenAPI paths (request-otp, verify-otp, plural participations, POST /v1/signals)", ["task", "backend"]),
                    ("Add all 17 required /v1/* paths with correct HTTP methods", ["task", "backend"]),
                    ("Update SignalCandidate schema with conditionConfidence, locationSource, locationPrecisionType", ["task", "backend"]),
                    ("Add HomeFeedResponse and HomeFeedClusterItem inline schemas", ["task", "backend"]),
                    ("Validate OpenAPI file with swagger-parser or equivalent", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 0.2 — Scaffold EF Core migrations for all 21 tables",
                "body": """**As** a developer,
**I need** EF Core migration files for every table
**So that** `dotnet ef database update` creates the full schema from scratch.

**Acceptance criteria:**
- One migration set per module (Auth, Signals, Clusters, Participation, Advisories, Notifications, Admin)
- All 17 indexes including 5 GiST and 1 partial index present
- Running migrations on a blank DB produces schema matching 01_postgres_schema.sql
- No raw SQL applied directly

**Ref:** schema_patch_notes.md, sync_prompt.md Task 2""",
                "labels": ["user-story", "backend", "infra", "session-00"],
                "tasks": [
                    ("Auth module migration: accounts, devices, otp_challenges, refresh_tokens", ["task", "backend"]),
                    ("Signals module migration: localities, location_labels, taxonomy_*, signal_events", ["task", "backend"]),
                    ("Clusters module migration: signal_clusters, cluster_event_links, civis_decisions, outbox_events", ["task", "backend"]),
                    ("Participation module migration: participations", ["task", "backend"]),
                    ("Advisories module migration: official_posts, official_post_scopes, institutions, institution_jurisdictions", ["task", "backend"]),
                    ("Notifications module migration: notifications, follows", ["task", "backend"]),
                    ("Admin module migration: admin_audit_logs", ["task", "backend"]),
                    ("Verify all GiST indexes present in migrations (5 total)", ["task", "testing"]),
                    ("Run migrations on blank DB and diff against 01_postgres_schema.sql", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 0.3 — Seed taxonomy (categories + conditions)",
                "body": """**As** the NLP validation layer,
**I need** all taxonomy tables populated
**So that** condition_slug and subcategory_slug lookups succeed for all 8 categories.

**Acceptance criteria:**
- 28 subcategory rows in taxonomy_categories
- 19 condition rows in taxonomy_conditions (all 8 categories covered)
- ON CONFLICT guards — safe to re-run
- Only seed_taxonomy_expanded.sql is run (not 03_seed_taxonomy.sql)

**Ref:** seed_taxonomy_expanded.sql""",
                "labels": ["user-story", "backend", "infra", "session-00"],
                "tasks": [
                    ("Run seed_taxonomy_expanded.sql and verify row counts (28 categories, 19 conditions)", ["task", "infra"]),
                    ("Confirm ON CONFLICT re-run is idempotent", ["task", "testing"]),
                ],
            },
        ],
    },

    # ── EPIC 1: Platform Foundations ──────────────────────────────────────
    {
        "epic": {
            "title": "Epic 1 — Platform Foundations",
            "description": "Scaffold solution structure, module boundaries, EF Core wiring, CI pipeline, and local dev stack.",
            "milestone": "01 — Foundation",
            "labels": ["epic", "infra", "session-01"],
        },
        "stories": [
            {
                "title": "Story 1.1 — Scaffold solution and module projects",
                "body": """**As** a developer,
**I need** the full solution structure created
**So that** all subsequent phases have a clean home.

**Acceptance criteria:**
- Projects: Hali.Api, Hali.Application, Hali.Domain, Hali.Infrastructure, Hali.Workers, Hali.Contracts
- Test projects: unit and integration
- `dotnet build` passes with zero errors
- Folder layout matches Folder_Structure.md

**Ref:** Folder_Structure.md, claude.md Architecture section""",
                "labels": ["user-story", "infra", "session-01"],
                "tasks": [
                    ("Create solution file and all 6 src projects", ["task", "infra"]),
                    ("Create test projects (unit, integration)", ["task", "infra"]),
                    ("Add project references (Api → Application → Domain, Infrastructure → Domain)", ["task", "infra"]),
                    ("Add .gitignore, README.md with project overview", ["task", "infra"]),
                    ("Verify dotnet build passes with zero errors", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 1.2 — Wire EF Core DbContext and confirm migrations apply",
                "body": """**As** a developer,
**I need** EF Core wired to PostgreSQL and migrations applied
**So that** all 21 tables exist in the dev database.

**Acceptance criteria:**
- HaliDbContext configured with all entity type configurations
- PostGIS / NetTopologySuite registered for geometry columns
- `dotnet ef database update` runs cleanly on a fresh DB
- All 21 tables visible in the database

**Ref:** 01_postgres_schema.sql, schema_patch_notes.md""",
                "labels": ["user-story", "backend", "infra", "session-01"],
                "tasks": [
                    ("Configure HaliDbContext with PostgreSQL and Npgsql", ["task", "backend"]),
                    ("Register NetTopologySuite for PostGIS geometry columns", ["task", "backend"]),
                    ("Apply all module migrations and verify 21 tables created", ["task", "testing"]),
                    ("Verify all 17 indexes present post-migration", ["task", "testing"]),
                ],
            },
        ],
    },

    # ── EPIC 2: Auth ───────────────────────────────────────────────────────
    {
        "epic": {
            "title": "Epic 2 — Identity and Access",
            "description": "OTP auth via Africa's Talking, JWT issuance, refresh token rotation, device registration, rate limiting.",
            "milestone": "02 — Auth",
            "labels": ["epic", "auth", "backend", "session-02"],
        },
        "stories": [
            {
                "title": "Story 2.1 — OTP request and verification flow",
                "body": """**As** a citizen,
**I need** to log in with my phone number
**So that** I can access Hali without a password.

**Acceptance criteria:**
- POST /v1/auth/otp sends 6-digit OTP via Africa's Talking
- OTP stored as bcrypt hash in otp_challenges with 10-minute TTL
- POST /v1/auth/verify validates OTP, marks consumed_at
- Returns access_token (60 min) + refresh_token (30 days)
- Rate limit: rl:otp:{destination} — max 5 requests per 10 min
- ISmsProvider interface wraps Africa's Talking

**Ref:** session_02_auth.md, mvp_locked_decisions.md §3""",
                "labels": ["user-story", "auth", "backend", "session-02"],
                "tasks": [
                    ("Implement ISmsProvider interface + Africa's Talking adapter", ["task", "auth", "backend"]),
                    ("OTP generation: 6-digit, bcrypt-hashed, stored in otp_challenges", ["task", "auth", "backend"]),
                    ("POST /v1/auth/otp endpoint with Redis rate limiting", ["task", "auth", "backend"]),
                    ("POST /v1/auth/verify endpoint — validate hash, mark consumed_at", ["task", "auth", "backend"]),
                    ("Unit tests: OTP expiry, consumed OTP rejection, rate limit enforcement", ["task", "testing", "auth"]),
                ],
            },
            {
                "title": "Story 2.2 — JWT and refresh token lifecycle",
                "body": """**As** a mobile client,
**I need** access tokens that refresh without re-authenticating
**So that** users are not logged out every 60 minutes.

**Acceptance criteria:**
- Access tokens: JWT HS256, 60-minute expiry
- Refresh tokens: stored as SHA-256 hash in refresh_tokens table
- POST /v1/auth/refresh: validates hash, issues new token pair (rotation)
- POST /v1/auth/logout: sets revoked_at on refresh token
- Expired or revoked tokens rejected with HTTP 401
- Rate limit: rl:auth-refresh:{deviceHash}

**Ref:** session_02_auth.md, claude.md Auth Rules""",
                "labels": ["user-story", "auth", "backend", "session-02"],
                "tasks": [
                    ("JWT service: issue access token from account + device claims", ["task", "auth", "backend"]),
                    ("Refresh token service: hash, store, rotate, revoke", ["task", "auth", "backend"]),
                    ("POST /v1/auth/refresh endpoint with rotation logic", ["task", "auth", "backend"]),
                    ("POST /v1/auth/logout endpoint — revoke by device", ["task", "auth", "backend"]),
                    ("Unit tests: token rotation, revocation, expired token rejection", ["task", "testing", "auth"]),
                ],
            },
            {
                "title": "Story 2.3 — Device registration",
                "body": """**As** the CIVIS engine,
**I need** each device fingerprint registered
**So that** device diversity checks work correctly.

**Acceptance criteria:**
- devices table upserted on every successful auth verify
- expo_push_token column nullable, populated when push token registered
- device_fingerprint_hash unique constraint enforced
- is_blocked flag respected on auth attempts

**Ref:** 01_postgres_schema.sql devices table""",
                "labels": ["user-story", "auth", "backend", "session-02"],
                "tasks": [
                    ("Device upsert logic on OTP verify (upsert by fingerprint hash)", ["task", "backend"]),
                    ("Blocked device check on auth (return 403 if is_blocked=true)", ["task", "backend"]),
                    ("Unit tests: device upsert idempotency, blocked device rejection", ["task", "testing"]),
                ],
            },
        ],
    },

    # ── EPIC 3: Signal Ingestion ───────────────────────────────────────────
    {
        "epic": {
            "title": "Epic 3 — Civic Signal Ingestion",
            "description": "NLP-first signal creation: free text → structured extraction → user confirmation → persistence.",
            "milestone": "03 — Signal Intake",
            "labels": ["epic", "nlp", "backend", "session-03"],
        },
        "stories": [
            {
                "title": "Story 3.1 — CSI-NLP extraction via Anthropic Claude API",
                "body": """**As** a citizen,
**I need** my free-text observation converted to a structured civic signal
**So that** Hali can cluster and display it accurately.

**Acceptance criteria:**
- POST /v1/signals/preview calls Anthropic Claude API with prompt from nlp_extraction_prompt.md
- Taxonomy injected into [APPLICATION SHOULD INJECT CANONICAL TAXONOMY HERE] placeholder
- Response parsed and validated: reject unknown categories, reject malformed JSON
- Field mapping applied per nlp_extraction_prompt.md mapping table:
  subcategory → subcategorySlug, condition_level → conditionSlug, etc.
- Confidence thresholds enforced (≥0.80 prefill / 0.50–0.79 confirmation / <0.50 correction)
- INlpExtractionService interface wraps the API call

**Ref:** nlp_extraction_prompt.md, session_03_intake.md""",
                "labels": ["user-story", "nlp", "backend", "session-03"],
                "tasks": [
                    ("INlpExtractionService interface + Anthropic Claude implementation", ["task", "nlp", "backend"]),
                    ("Build and inject prompt template with canonical taxonomy", ["task", "nlp", "backend"]),
                    ("NLP response DTO: parse, validate category, validate subcategory compatibility", ["task", "nlp", "backend"]),
                    ("Field name mapping layer: NLP JSON → API DTO → domain entity", ["task", "nlp", "backend"]),
                    ("Confidence threshold routing logic (prefill / confirm / correct)", ["task", "nlp", "backend"]),
                    ("POST /v1/signals/preview endpoint wired to NLP service", ["task", "backend"]),
                    ("Unit tests: valid extraction, unknown category rejection, malformed JSON rejection", ["task", "testing", "nlp"]),
                ],
            },
            {
                "title": "Story 3.2 — Signal submission and persistence",
                "body": """**As** a citizen,
**I need** my confirmed signal stored durably
**So that** it contributes to clustering and CIVIS scoring.

**Acceptance criteria:**
- POST /v1/signals/submit persists signal_event with all NLP fields
- lat/lng, location_confidence, location_source, condition_confidence all stored
- spatial_cell_id computed from lat/lng using H3 resolution 9
- Idempotency: idem:signal-submit:{idempotencyKey} checked in Redis before insert
- outbox_events row written on every new signal_event
- Rate limit: rl:signal-submit:{deviceHash}

**Ref:** session_03_intake.md, mvp_locked_decisions.md §5""",
                "labels": ["user-story", "backend", "session-03"],
                "tasks": [
                    ("H3.net integration: lat/lng → spatial_cell_id at resolution 9", ["task", "backend"]),
                    ("Signal event persistence with all NLP output fields", ["task", "backend"]),
                    ("Redis idempotency check and write (idem:signal-submit)", ["task", "backend"]),
                    ("Outbox event write on signal_event insert", ["task", "backend"]),
                    ("POST /v1/signals/submit endpoint", ["task", "backend"]),
                    ("Unit tests: idempotency (same key = one row), H3 cell computation", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 3.3 — Geocoding and location label resolution",
                "body": """**As** the signal pipeline,
**I need** lat/lng resolved to a human-readable location label
**So that** citizens see meaningful location names not coordinates.

**Acceptance criteria:**
- IGeocodingService interface wraps Nominatim/OpenStreetMap
- Reverse geocoding: lat/lng → area_name, road_name, junction_name, landmark_name
- Results cached in Redis (key: geocode:{lat_trunc}:{lng_trunc})
- Vendor response shapes not exposed to domain layer
- Location label stored in location_labels table and linked via location_label_id

**Ref:** session_03_intake.md, mvp_locked_decisions.md §6""",
                "labels": ["user-story", "backend", "session-03"],
                "tasks": [
                    ("IGeocodingService interface + Nominatim implementation", ["task", "backend"]),
                    ("Reverse geocoding: lat/lng → location_label fields", ["task", "backend"]),
                    ("Redis geocoding cache (key: geocode:{lat}:{lng})", ["task", "backend"]),
                    ("Location label upsert into location_labels table", ["task", "backend"]),
                    ("Unit tests: cache hit/miss, vendor shape isolation", ["task", "testing"]),
                ],
            },
        ],
    },

    # ── EPIC 4: Clustering and CIVIS ──────────────────────────────────────
    {
        "epic": {
            "title": "Epic 4 — Clustering and CIVIS",
            "description": "H3-based cluster matching, join-score algorithm, WRAB/SDS/MACF activation logic, decay, and state transitions.",
            "milestone": "04 — Clustering + CIVIS",
            "labels": ["epic", "civis", "backend", "session-04"],
        },
        "stories": [
            {
                "title": "Story 4.1 — H3 candidate search and join-score clustering",
                "body": """**As** the clustering service,
**I need** new signals routed to existing clusters or new ones created
**So that** civic conditions are represented as unified clusters, not individual reports.

**Acceptance criteria:**
- H3 cell + k-ring-1 neighbors used to find candidate clusters
- join_score = 0.40×category + 0.25×distance + 0.20×time + 0.15×condition
- Join if join_score >= CIVIS_JOIN_THRESHOLD (0.65)
- cluster_event_links row written on every join
- New signal_cluster created (state=unconfirmed) when no match
- outbox_events written on cluster create and update

**Ref:** session_04_clustering_civis.md, mvp_locked_decisions.md §5 and §9""",
                "labels": ["user-story", "civis", "backend", "session-04"],
                "tasks": [
                    ("H3 k-ring-1 neighbor search for candidate clusters", ["task", "civis", "backend"]),
                    ("Join-score formula implementation (4-component weighted sum)", ["task", "civis", "backend"]),
                    ("Cluster join: insert cluster_event_links, update last_seen_at and raw_confirmation_count", ["task", "civis", "backend"]),
                    ("New cluster creation: state=unconfirmed, all fields populated", ["task", "civis", "backend"]),
                    ("Outbox event emission on cluster create/update", ["task", "civis", "backend"]),
                    ("Unit tests: join at exactly threshold, below threshold, no candidates", ["task", "testing", "civis"]),
                ],
            },
            {
                "title": "Story 4.2 — CIVIS scoring: WRAB, SDS, MACF",
                "body": """**As** the CIVIS engine,
**I need** to compute contextual signal density scores
**So that** cluster activation thresholds adapt to local baseline activity.

**Acceptance criteria:**
- WRAB = Σ(weighted_active_signal_events over CIVIS_WRAB_ROLLING_WINDOW_DAYS)
- effective_WRAB = max(WRAB, base_floor[category])
- SDS = active_mass_now / effective_WRAB
- MACF = clamp(ceil(base_floor + log2(SDS + 1)), macf_min, macf_max)
- All constants read from configuration (no hardcoded values)
- civis_decisions row written with reason_codes on every evaluation

**Ref:** mvp_locked_decisions.md §9 CIVIS Formulas""",
                "labels": ["user-story", "civis", "backend", "session-04"],
                "tasks": [
                    ("WRAB computation service (rolling window, per category)", ["task", "civis", "backend"]),
                    ("SDS computation (active mass / effective_WRAB)", ["task", "civis", "backend"]),
                    ("MACF computation (clamp formula, per-category min/max from config)", ["task", "civis", "backend"]),
                    ("civis_decisions persistence with reason_codes JSONB", ["task", "civis", "backend"]),
                    ("Unit tests: MACF at SDS=0/1/5, all 7 categories, clamp boundaries", ["task", "testing", "civis"]),
                    ("Unit tests: effective_WRAB floor prevents zero-baseline hyper-activation", ["task", "testing", "civis"]),
                ],
            },
            {
                "title": "Story 4.3 — Activation gate, decay, and lifecycle transitions",
                "body": """**As** the cluster lifecycle engine,
**I need** clusters to activate when evidence is sufficient and decay when stale
**So that** the public feed shows only real, current conditions.

**Acceptance criteria:**
- Activation: raw_confirmation_count >= MACF AND DISTINCT device_count >= CIVIS_MIN_UNIQUE_DEVICES
- Decay: exponential with lambda = ln(2) / half_life_hours per category
- DecayActiveClustersJob runs every 5 minutes
- EvaluatePossibleRestorationJob runs every 5 minutes
- Every state transition writes outbox_event and civis_decisions record
- State machine: unconfirmed → active → possible_restoration → resolved

**Ref:** session_04_clustering_civis.md, 05_redis_job_config.md""",
                "labels": ["user-story", "civis", "backend", "session-04"],
                "tasks": [
                    ("Activation gate: MACF check + device diversity check", ["task", "civis", "backend"]),
                    ("Decay worker: lambda per category, deactivation threshold", ["task", "civis", "backend"]),
                    ("State transition service: writes civis_decisions + outbox_event", ["task", "civis", "backend"]),
                    ("DecayActiveClustersJob scheduled every 5 minutes", ["task", "civis", "backend"]),
                    ("Unit tests: activation pass/fail, decay at t=half_life, device diversity gate", ["task", "testing", "civis"]),
                    ("Unit tests: same device repeated counts as 1 distinct device", ["task", "testing", "civis"]),
                ],
            },
        ],
    },

    # ── EPIC 5: Participation and Restoration ────────────────────────────
    {
        "epic": {
            "title": "Epic 5 — Participation and Restoration",
            "description": "Affected/Observing participation, Add Further Context gate, restoration voting, and cluster resolution.",
            "milestone": "05 — Participation + Vertical Slice",
            "labels": ["epic", "backend", "session-05"],
        },
        "stories": [
            {
                "title": "Story 5.1 — Participation endpoints (Affected / Observing / No Longer Affected)",
                "body": """**As** a citizen near a civic condition,
**I need** to mark my relationship to a cluster
**So that** my signal contributes to CIVIS scoring and restoration decisions.

**Acceptance criteria:**
- POST /v1/clusters/{id}/participation accepts: affected, observing, no_longer_affected, restoration_yes, restoration_no, restoration_unsure
- One active type per device per cluster enforced (replace, not append)
- Redis idempotency: idem:participation:{idempotencyKey}
- signal_clusters.affected_count and observing_count updated after each write
- outbox_event written on every participation

**Ref:** session_05_participation_slice.md, claude.md Application-Layer Business Rules""",
                "labels": ["user-story", "backend", "session-05"],
                "tasks": [
                    ("Participation write service: upsert with one-type-per-device enforcement", ["task", "backend"]),
                    ("Redis idempotency for participation endpoint", ["task", "backend"]),
                    ("Denormalised count maintenance: affected_count and observing_count on signal_clusters", ["task", "backend"]),
                    ("POST /v1/clusters/{id}/participation endpoint", ["task", "backend"]),
                    ("Unit tests: second participation type replaces first, idempotency key deduplication", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 5.2 — Add Further Context (gate + edit window)",
                "body": """**As** an affected citizen,
**I need** to add more detail to my signal after marking I'm Affected
**So that** the cluster has richer context without becoming a free-form post.

**Acceptance criteria:**
- POST /v1/clusters/{id}/context only accepted if device has active 'affected' participation
- Rejected with HTTP 422 / code: context_requires_affected_participation otherwise
- Edit window: only accepted within CIVIS_CONTEXT_EDIT_WINDOW_MINUTES (2) of affected participation
- Rejected with HTTP 422 / code: context_edit_window_expired after window
- context_text stored on participations row, max 150 chars

**Ref:** claude.md Application-Layer Business Rules""",
                "labels": ["user-story", "backend", "session-05"],
                "tasks": [
                    ("Affected participation gate check for context endpoint", ["task", "backend"]),
                    ("Edit window enforcement using participation.created_at + CIVIS_CONTEXT_EDIT_WINDOW_MINUTES", ["task", "backend"]),
                    ("POST /v1/clusters/{id}/context endpoint", ["task", "backend"]),
                    ("Unit tests: context rejected without affected, rejected after window, accepted within window", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 5.3 — Restoration voting and cluster resolution",
                "body": """**As** an affected citizen,
**I need** to confirm when a condition is resolved
**So that** clusters close accurately without relying solely on institutional claims.

**Acceptance criteria:**
- POST /v1/clusters/{id}/restoration-response records vote (restored / still_affected / not_sure)
- EvaluatePossibleRestorationJob checks: restore_votes / total_affected >= 0.60 AND restore_votes >= 2
- Cluster transitions to resolved when threshold met
- Cluster reverts to active if still_affected votes counter restoration
- Restoration threshold constants from config (CIVIS_RESTORATION_RATIO, CIVIS_MIN_RESTORATION_AFFECTED_VOTES)

**Ref:** session_05_participation_slice.md, mvp_locked_decisions.md §9""",
                "labels": ["user-story", "backend", "session-05"],
                "tasks": [
                    ("Restoration response endpoint: map to participation_type (restoration_yes/no/unsure)", ["task", "backend"]),
                    ("Restoration evaluation formula: ratio + minimum vote count check", ["task", "backend"]),
                    ("Cluster revert to active when still_affected votes arrive", ["task", "backend"]),
                    ("Unit tests: resolution at exactly 60%, below 60%, revert on counter-votes", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 5.4 — Vertical slice integration test (13-step flow)",
                "body": """**As** a QA engineer,
**I need** the full civic loop validated end-to-end
**So that** official updates and notifications are built on a proven foundation.

**Acceptance criteria (all 13 steps must pass):**
1. Auth: POST /v1/auth/otp + POST /v1/auth/verify returns tokens
2. Signal preview: "Big potholes near National Oil, Nairobi West" → category=roads
3. Signal submit: signal_event row created with lat/lng and NLP confidence fields
4. Cluster created: signal_cluster with state=unconfirmed
5. Second + third signals from different device fingerprints submitted
6. CIVIS activation: cluster state transitions to active
7. Participation: POST /v1/clusters/{id}/participation type=affected succeeds
8. Context: POST /v1/clusters/{id}/context accepted within window
9. Context rejected: same endpoint rejected after CIVIS_CONTEXT_EDIT_WINDOW_MINUTES
10. GET /v1/clusters/{id}: returns state=active, affected_count=1
11. Restoration response: POST /v1/clusters/{id}/restoration-response type=restored
12. GET /v1/clusters/{id}: state=possible_restoration
13. GET /v1/home: cluster appears in activeNow section

**Ref:** session_05_participation_slice.md""",
                "labels": ["user-story", "testing", "session-05"],
                "tasks": [
                    ("Write vertical slice integration test fixture (seeded DB + test client)", ["task", "testing"]),
                    ("Steps 1–4: auth and first signal", ["task", "testing"]),
                    ("Steps 5–6: CIVIS activation with multiple devices", ["task", "testing"]),
                    ("Steps 7–10: participation, context gate, cluster detail read", ["task", "testing"]),
                    ("Steps 11–13: restoration and home feed appearance", ["task", "testing"]),
                ],
            },
        ],
    },

    # ── EPIC 6: Official Updates ───────────────────────────────────────────
    {
        "epic": {
            "title": "Epic 6 — Official Updates",
            "description": "Institution post creation, geo-scope enforcement, dual-visibility rendering, official expiry.",
            "milestone": "06 — Updates + Restoration",
            "labels": ["epic", "backend", "session-06"],
        },
        "stories": [
            {
                "title": "Story 6.1 — Institution post creation with geo-scope enforcement",
                "body": """**As** an institution,
**I need** to publish updates to the wards I serve
**So that** citizens see authoritative information alongside citizen signals.

**Acceptance criteria:**
- POST /v1/official-posts creates official_posts + official_post_scopes rows
- Geo-scope enforcement: post scope must intersect institution jurisdiction (ST_Intersects)
- Rejected with HTTP 403 / code: outside_jurisdiction if out of scope
- Post types: live_update, scheduled_disruption, advisory_public_notice
- Related cluster linkage optional (relatedClusterId)
- ExpireOfficialPostsJob sets status=expired when ends_at < NOW()

**Ref:** session_06_updates_restoration.md""",
                "labels": ["user-story", "backend", "session-06"],
                "tasks": [
                    ("Official post creation service: posts + scopes in one transaction", ["task", "backend"]),
                    ("Geo-scope enforcement: ST_Intersects check with GiST index", ["task", "backend"]),
                    ("ExpireOfficialPostsJob scheduled every 10 minutes", ["task", "backend"]),
                    ("POST /v1/official-posts endpoint", ["task", "backend"]),
                    ("Unit tests: out-of-scope rejection, expiry job transitions", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 6.2 — Dual-visibility rendering in cluster detail and home feed",
                "body": """**As** a citizen,
**I need** to see both citizen signals and institutional responses in one view
**So that** I can understand both the ground truth and the official response.

**Acceptance criteria:**
- GET /v1/clusters/{id} returns official_posts array alongside cluster data
- GET /v1/home officialUpdates section returns published posts for followed wards
- Official posts NEVER suppress or replace citizen cluster state
- institution is_restoration_claim=true triggers possible_restoration on linked cluster — not resolution

**Ref:** claude.md Core Product Doctrine §2 Dual Visibility""",
                "labels": ["user-story", "backend", "session-06"],
                "tasks": [
                    ("Cluster detail query: join official_posts for related cluster", ["task", "backend"]),
                    ("Home feed official updates section: published posts for followed wards", ["task", "backend"]),
                    ("Restoration claim handler: sets cluster to possible_restoration only", ["task", "backend"]),
                    ("Integration test: official post does not override cluster state to resolved", ["task", "testing"]),
                ],
            },
        ],
    },

    # ── EPIC 7: Notifications ──────────────────────────────────────────────
    {
        "epic": {
            "title": "Epic 7 — Notifications and Background Jobs",
            "description": "Expo push notifications, ward following, notification worker, restoration prompts.",
            "milestone": "07 — Notifications + Polish",
            "labels": ["epic", "backend", "session-07"],
        },
        "stories": [
            {
                "title": "Story 7.1 — Push token registration and ward following",
                "body": """**As** a citizen,
**I need** to register my device for push notifications and follow up to 5 wards
**So that** I receive timely alerts about conditions in my area.

**Acceptance criteria:**
- POST /v1/devices/push-token stores Expo token in devices.expo_push_token
- GET /v1/localities/followed returns followed wards
- PUT /v1/localities/followed replaces full set, enforces max 5
- Max-5 rejected with HTTP 422 / code: max_followed_localities_exceeded
- PUT /v1/users/me/notification-settings persists opt-in preferences
- GET /v1/users/me returns account summary

**Ref:** session_07_notifications_polish.md, claude.md Application-Layer Business Rules""",
                "labels": ["user-story", "backend", "session-07"],
                "tasks": [
                    ("POST /v1/devices/push-token: upsert expo_push_token on devices row", ["task", "backend"]),
                    ("PUT /v1/localities/followed: replace set, enforce max-5 gate", ["task", "backend"]),
                    ("PUT /v1/users/me/notification-settings endpoint", ["task", "backend"]),
                    ("GET /v1/users/me endpoint (account + device summary)", ["task", "backend"]),
                    ("Unit tests: max-5 enforcement, push token upsert idempotency", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 7.2 — Notification delivery via Expo Push API",
                "body": """**As** a followed citizen,
**I need** push notifications when clusters activate, restore, or resolve
**So that** I am informed without having to poll the app.

**Acceptance criteria:**
- IPushNotificationService interface wraps Expo Push API
- Notification types: cluster_activated, restoration_prompt, cluster_resolved
- dedupe_key prevents duplicate notifications
- Notification worker: SELECT WHERE status='queued' AND send_after<=NOW() (partial index used)
- Delivery recorded in notifications.sent_at + status update
- Dead-letter: failed notifications after 3 retries raise alert log

**Ref:** session_07_notifications_polish.md, mvp_locked_decisions.md §7""",
                "labels": ["user-story", "backend", "session-07"],
                "tasks": [
                    ("IPushNotificationService interface + Expo Push API implementation", ["task", "backend"]),
                    ("Notification worker: query queued notifications, deliver, update status", ["task", "backend"]),
                    ("Dedupe key enforcement on notifications table", ["task", "backend"]),
                    ("cluster_activated notification: fan-out to followers of affected ward", ["task", "backend"]),
                    ("restoration_prompt notification: fan-out to affected cohort only", ["task", "backend"]),
                    ("cluster_resolved notification: fan-out to followers", ["task", "backend"]),
                    ("Unit tests: delivery deduplication, dead-letter after 3 failures", ["task", "testing"]),
                ],
            },
            {
                "title": "Story 7.3 — Final integration test and dotnet test clean",
                "body": """**As** the build system,
**I need** all tests passing and the full civic loop verified with notifications
**So that** the MVP is production-ready.

**Acceptance criteria:**
- `dotnet test` across all projects = 0 failures
- Full vertical slice runs with push notifications enabled
- GET /v1/home returns correct 4-section response (activeNow, officialUpdates, recurringAtThisTime, otherActiveSignals)
- No TODO or placeholder implementations remaining in core paths

**Ref:** session_07_notifications_polish.md""",
                "labels": ["user-story", "testing", "session-07"],
                "tasks": [
                    ("Full vertical slice with push: signal → activate → notify → restore → resolve → notify", ["task", "testing"]),
                    ("GET /v1/home: verify all 4 sections populated correctly", ["task", "testing"]),
                    ("Run dotnet test — resolve all failures", ["task", "testing"]),
                    ("Smoke test: docker-compose up + seed + migrate + API healthcheck", ["task", "infra", "testing"]),
                ],
            },
        ],
    },
]

# ─── GitHub automation ────────────────────────────────────────────────────────

def ensure_label(repo, name, colour, description=""):
    try:
        return repo.get_label(name)
    except GithubException:
        return repo.create_label(name, colour, description)

def ensure_milestone(repo, title, description=""):
    for m in repo.get_milestones(state="open"):
        if m.title == title:
            return m
    return repo.create_milestone(title, description=description)

def create_issue(repo, title, body, labels, milestone=None, parent_number=None):
    if parent_number:
        body = f"_Part of #{parent_number}_\n\n{body}"
    label_objs = [repo.get_label(l) for l in labels]
    kwargs = dict(title=title, body=body, labels=label_objs)
    if milestone:
        kwargs["milestone"] = milestone
    issue = repo.create_issue(**kwargs)
    time.sleep(0.5)  # avoid secondary rate limit
    return issue

def run():
    g    = Github(GITHUB_TOKEN)
    repo = g.get_repo(GITHUB_REPO)

    print(f"Connected to: {repo.full_name}")
    print("Creating labels...")
    for name, colour in LABEL_COLOURS.items():
        ensure_label(repo, name, colour)
        print(f"  ✓ label: {name}")

    for epic_block in ISSUE_TREE:
        e = epic_block["epic"]
        print(f"\nCreating milestone: {e['milestone']}")
        milestone = ensure_milestone(repo, e["milestone"], e["description"])

        print(f"Creating epic issue: {e['title']}")
        epic_issue = create_issue(
            repo,
            title=f"🏛 {e['title']}",
            body=f"## {e['title']}\n\n{e['description']}\n\n---\n_This is the Epic issue. All user stories for this epic are linked below._",
            labels=e["labels"],
            milestone=milestone,
        )
        print(f"  ✓ Epic #{epic_issue.number}: {e['title']}")

        for story in epic_block["stories"]:
            print(f"  Creating story: {story['title']}")
            story_issue = create_issue(
                repo,
                title=f"📖 {story['title']}",
                body=story["body"],
                labels=story["labels"],
                milestone=milestone,
                parent_number=epic_issue.number,
            )
            print(f"    ✓ Story #{story_issue.number}: {story['title']}")

            for task_title, task_labels in story["tasks"]:
                task_issue = create_issue(
                    repo,
                    title=f"☑ {task_title}",
                    body=f"_Task for #{story_issue.number}: {story['title']}_",
                    labels=task_labels,
                    milestone=milestone,
                    parent_number=story_issue.number,
                )
                print(f"      ✓ Task #{task_issue.number}: {task_title}")

    print("\n✅ All issues created successfully.")
    print(f"   View at: https://github.com/{GITHUB_REPO}/issues")

if __name__ == "__main__":
    run()

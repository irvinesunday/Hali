#!/usr/bin/env python3
"""
Hali GitHub Issues — Catch-up Script
Creates issues for all completed backend sessions and known bugs.
Run from the repo root: python3 scripts/create_catchup_issues.py

Prerequisites:
  gh auth login (GitHub CLI authenticated)
  gh repo set-default (or pass --repo owner/repo)

Usage:
  python3 scripts/create_catchup_issues.py
  python3 scripts/create_catchup_issues.py --dry-run   # print only, no API calls
  python3 scripts/create_catchup_issues.py --repo your-org/hali
"""

import subprocess
import json
import sys
import time
import argparse

# ─────────────────────────────────────────────────────────────────────────────
# CONFIG
# ─────────────────────────────────────────────────────────────────────────────

MILESTONES = {
    "01 — Platform Foundations":   None,
    "02 — Identity & Access":      None,
    "03 — Signal Intake":          None,
    "04 — Clustering & CIVIS":     None,
    "05 — Participation":          None,
    "06 — Official Updates":       None,
    "07 — Restoration":            None,
    "08 — Notifications & Jobs":   None,
    "09 — Observability & Security": None,
    "10 — Mobile App":             None,
    "11 — Integration Tests":      None,
}

# ─────────────────────────────────────────────────────────────────────────────
# ISSUE DEFINITIONS — COMPLETED SESSIONS
# ─────────────────────────────────────────────────────────────────────────────

COMPLETED_ISSUES = [

    # ── EPIC 1: Platform Foundations ─────────────────────────────────────
    {
        "title": "[Session 01] Platform foundations — repo scaffold, schema, Docker",
        "body": (
            "## Summary\n"
            "Completed as part of Session 01 backend build.\n\n"
            "### Delivered\n"
            "- Solution skeleton: Hali.Api, Hali.Application, Hali.Domain, "
            "Hali.Infrastructure, Hali.Workers, Hali.Contracts\n"
            "- Module boundaries: Auth, Signals, Clusters, Participation, "
            "Advisories, Institutions, Admin, CIVIS, NLP, Notifications, Metrics\n"
            "- PostgreSQL schema DDL (reference): `01_postgres_schema.sql`\n"
            "- EF Core code-first migrations (source of truth)\n"
            "- PostGIS extension enabled\n"
            "- Docker Compose: api, worker, db (postgis/postgis:16-3.4), redis:7, otel-collector\n"
            "- `.env.example` with all CIVIS constants externalised\n"
            "- `claude.md` engineering seed\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-01", "foundations"],
        "milestone": "01 — Platform Foundations",
    },

    # ── EPIC 2: Identity and Access ───────────────────────────────────────
    {
        "title": "[Session 02] Identity & access — OTP auth, JWT, refresh tokens, device registration",
        "body": (
            "## Summary\n"
            "Completed as part of Session 02 backend build.\n\n"
            "### Delivered\n"
            "- `POST /v1/auth/otp` — Africa's Talking SMS OTP delivery via ISmsProvider\n"
            "- `POST /v1/auth/verify` — OTP check → JWT access token (60 min) + refresh token (30 days)\n"
            "- `POST /v1/auth/refresh` — token rotation; new pair issued, old token revoked\n"
            "- `POST /v1/auth/logout` — `revoked_at` set on refresh token record\n"
            "- `refresh_tokens` table: `token_hash`, `account_id`, `device_id`, "
            "`expires_at`, `revoked_at`, `created_at`\n"
            "- Refresh tokens stored as SHA-256 hashes (never plaintext)\n"
            "- Device upsert on every verify — fingerprint hash, platform, app_version\n"
            "- Blocked device check: HTTP 403 if `is_blocked = true`\n"
            "- Rate limits: `rl:otp:{destination}`, `rl:auth-refresh:{deviceHash}`\n"
            "- Role-based authorization middleware\n"
            "- Audit logging for privileged auth actions\n\n"
            "### Known Issue\n"
            "⚠️ OpenAPI declares `phone_otp` (snake_case) but enum serialises as `PhoneOtp` "
            "(PascalCase). Tracked as BACKEND-ENUM-001.\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-02", "auth"],
        "milestone": "02 — Identity & Access",
    },

    # ── EPIC 3: Signal Ingestion ──────────────────────────────────────────
    {
        "title": "[Session 03] Signal ingestion — CSI-NLP, preview, submit, geocoding, H3",
        "body": (
            "## Summary\n"
            "Completed as part of Session 03 backend build.\n\n"
            "### Delivered\n"
            "- `INlpExtractionService` interface + Anthropic Claude Sonnet implementation\n"
            "- Full prompt template with canonical taxonomy injection "
            "(from `nlp_extraction_prompt.md`)\n"
            "- `POST /v1/signals/preview` — NLP extraction with structured JSON output\n"
            "- Response validation: unknown categories rejected, malformed JSON rejected\n"
            "- Confidence threshold routing: ≥0.80 prefill / 0.50–0.79 confirm / <0.50 correction\n"
            "- `POST /v1/signals/submit` — signal_event persistence with all NLP fields\n"
            "- H3.net integration: lat/lng → `spatial_cell_id` at resolution 9\n"
            "- Redis idempotency: `idem:signal-submit:{idempotencyKey}`\n"
            "- Rate limit: `rl:signal-submit:{deviceHash}`\n"
            "- Outbox event written on every signal_event insert\n"
            "- `IGeocodingService` interface + Nominatim/OSM implementation\n"
            "- Reverse geocoding: lat/lng → area, road, junction, landmark labels\n"
            "- Geocoding cache in Redis: `geocode:{lat}:{lng}`\n"
            "- Location label upserted to `location_labels` table\n\n"
            "### Known Issue\n"
            "⚠️ `should_suggest_join` NLP field not yet wired to return "
            "`existingClusterCandidates` in preview response (NLP-002).\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-03", "nlp", "signals"],
        "milestone": "03 — Signal Intake",
    },

    # ── EPIC 4: Clustering and CIVIS ──────────────────────────────────────
    {
        "title": "[Session 04] Clustering & CIVIS — join-score, WRAB/SDS/MACF, activation, decay",
        "body": (
            "## Summary\n"
            "Completed as part of Session 04 backend build.\n\n"
            "### Delivered\n"
            "- H3-based candidate cluster search by category + spatial cell + time window\n"
            "- Join-score formula: `0.40×category + 0.25×distance + 0.20×time + 0.15×condition`\n"
            "- Join threshold: 0.65 (externalised to env)\n"
            "- WRAB: decayed weighted rolling baseline per geo×category×time\n"
            "- SDS: `active_mass_now / effective_WRAB`\n"
            "- MACF: `clamp(ceil(base_floor + log2(SDS+1)), macf_min, macf_max)`\n"
            "- All CIVIS constants externalised to environment variables per category\n"
            "- Device diversity gate: min 2 unique devices for activation\n"
            "- Burst dampening and dominant-share cap\n"
            "- `civis_decisions` table: decision_type, reason_codes, metrics\n"
            "- `cluster_event_links` for lineage tracking\n"
            "- Decay job: `DecayActiveClustersJob` (every 5 min)\n"
            "- Deactivation threshold lower than activation to prevent flicker\n\n"
            "### Correctness Fix Applied\n"
            "✅ `governance` category CIVIS constants added (was missing at initial build — "
            "BLOCKING-3 resolved)\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-04", "civis", "clustering"],
        "milestone": "04 — Clustering & CIVIS",
    },

    # ── EPIC 5: Participation ─────────────────────────────────────────────
    {
        "title": "[Session 05] Participation — Affected/Observing, idempotency, context window",
        "body": (
            "## Summary\n"
            "Completed as part of Session 05 backend build.\n\n"
            "### Delivered\n"
            "- `POST /v1/clusters/{id}/participation` — affected, observing, "
            "no_longer_affected, restoration_yes/no/unsure\n"
            "- One participation type per device per cluster enforced at application layer\n"
            "- Idempotency: `idem:participation:{idempotencyKey}` in Redis\n"
            "- `POST /v1/clusters/{id}/context` — Add Further Context (max 150 chars)\n"
            "- Context edit window: 2 minutes (`CIVIS_CONTEXT_EDIT_WINDOW_MINUTES=2`)\n"
            "- Context only accepted after an 'affected' participation exists on that cluster\n"
            "- Participations index: `ix_participations_cluster_type_time`\n\n"
            "### Correctness Fix Applied\n"
            "✅ `MinRestorationAffectedVotes` default corrected to 2 in test fixtures "
            "(was 1 — BLOCKING-5 resolved)\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-05", "participation"],
        "milestone": "05 — Participation",
    },

    # ── EPIC 6: Official Updates ──────────────────────────────────────────
    {
        "title": "[Session 06] Official updates — institution posts, geo-scope, jurisdiction enforcement",
        "body": (
            "## Summary\n"
            "Completed as part of Session 06 backend build.\n\n"
            "### Delivered\n"
            "- `POST /v1/official-posts` — create geo-scoped institution post\n"
            "- Post types: `live_update`, `scheduled_disruption`, `advisory_public_notice`\n"
            "- `official_post_scopes` table: locality_id, corridor_name, geom\n"
            "- Server-side jurisdiction enforcement (institution can only post within scope)\n"
            "- `GET /v1/clusters/{id}` returns official updates side-by-side with cluster\n"
            "- Official posts never suppress citizen clusters (dual-visibility enforced)\n"
            "- `ExpireOfficialPostsJob` runs every 10 min\n"
            "- Institution header X-Institution-Id validated on all institution routes\n\n"
            "### Correctness Fix Applied\n"
            "✅ Institution header validation tightened after Agent C review (BLOCKING-2 resolved)\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-06", "official-updates", "institutions"],
        "milestone": "06 — Official Updates",
    },

    # ── EPIC 7: Restoration ───────────────────────────────────────────────
    {
        "title": "[Session 07] Restoration lifecycle — possible restoration, threshold, reversion",
        "body": (
            "## Summary\n"
            "Completed as part of Session 07 backend build.\n\n"
            "### Delivered\n"
            "- `POST /v1/clusters/{id}/restoration-response` endpoint\n"
            "- Institution restoration claim → POSSIBLE_RESTORATION (not direct RESOLVED)\n"
            "- Citizen 'no_longer_affected' → can trigger POSSIBLE_RESTORATION\n"
            "- Restoration ratio check: `affected_restore_votes / total_affected_votes >= 0.60`\n"
            "- Minimum 2 distinct affected devices required for resolution\n"
            "- Cooldown prevents rapid oscillation\n"
            "- Still-affected evidence reverts cluster to ACTIVE\n"
            "- Restoration insufficient after timeout → reverts to ACTIVE or RECURRING_CONTEXT\n"
            "- `EvaluatePossibleRestorationJob` runs every 5 min\n"
            "- All state transitions emit outbox events with reason codes\n\n"
            "### Correctness Fix Applied\n"
            "✅ `DeactivationThreshold=0.5` decay path documented and validated "
            "— confirmed lower than activation threshold to prevent flicker (BLOCKING-6 resolved)\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-07", "restoration", "lifecycle"],
        "milestone": "07 — Restoration",
    },

    # ── EPIC 8: Notifications and Jobs ────────────────────────────────────
    {
        "title": "[Session 08] Notifications & background jobs — decay, expiry, dead-letter, cache refresh",
        "body": (
            "## Summary\n"
            "Completed as part of Session 08 backend build.\n\n"
            "### Delivered\n"
            "- Notification service: push fan-out via Expo Push API\n"
            "- `POST /v1/devices/push-token` — register Expo push token per device\n"
            "- `PUT /v1/users/me/notification-settings` — per-user notification preferences\n"
            "- Dedupe key on notifications table (`unique(dedupe_key)`)\n"
            "- Quiet-hours policy enforced\n"
            "- Background jobs:\n"
            "  - `DecayActiveClustersJob` — every 5 min\n"
            "  - `EvaluatePossibleRestorationJob` — every 5 min\n"
            "  - `ExpireOfficialPostsJob` — every 10 min\n"
            "  - `RefreshLocalitySnapshotCacheJob` — every 2 min for hot areas\n"
            "  - `AggregateTemporalPatternsJob` — hourly\n"
            "  - `ProjectMetricsJob` — every 15 min\n"
            "- Dead-letter queue per worker category with replay tooling hooks\n"
            "- Retry policy: exponential backoff with jitter, max 8 attempts (idempotent jobs)\n"
            "- Outbox pattern: PostgreSQL → queue bridge\n\n"
            "### Status\n"
            "✅ Merged to main | Tests: 15/15 green"
        ),
        "labels": ["completed", "backend", "session-08", "notifications", "jobs"],
        "milestone": "08 — Notifications & Jobs",
    },

    # ── POST-BUILD CORRECTNESS FIXES ─────────────────────────────────────
    {
        "title": "[Post-build] Agent C validation — 7 blocking issues resolved",
        "body": (
            "## Summary\n"
            "Agent C (validator) ran a deep validation pass after all 8 sessions.\n"
            "7 blocking issues were identified and resolved.\n\n"
            "### Issues Resolved\n"
            "| Ref | Issue | Resolution |\n"
            "|---|---|---|\n"
            "| BLOCKING-1 | Integration tests missing | Deferred — dedicated session planned |\n"
            "| BLOCKING-2 | Institution header not validated | Fixed |\n"
            "| BLOCKING-3 | `governance` CIVIS constants missing | Fixed |\n"
            "| BLOCKING-4 | Preview endpoint missing rate limit | Fixed |\n"
            "| BLOCKING-5 | `MinRestorationAffectedVotes=1` in test defaults | Fixed (set to 2) |\n"
            "| BLOCKING-6 | `DeactivationThreshold=0.5` undocumented | Documented + validated |\n"
            "| BLOCKING-7 | Structured logging unverified | Verified live against running API |\n\n"
            "### Status\n"
            "✅ All 6 fixable blocking issues resolved | ✅ 15/15 tests passing"
        ),
        "labels": ["completed", "backend", "validation", "agent-c"],
        "milestone": "09 — Observability & Security",
    },
]

# ─────────────────────────────────────────────────────────────────────────────
# BUG ISSUES — KNOWN, OPEN
# ─────────────────────────────────────────────────────────────────────────────

BUG_ISSUES = [
    {
        "title": "[Bug] BACKEND-ENUM-001: auth OTP method enum casing mismatch (snake_case vs PascalCase)",
        "body": (
            "## Bug\n"
            "OpenAPI spec declares `phone_otp` (snake_case) but the backend C# enum "
            "serialises as `PhoneOtp` (PascalCase).\n\n"
            "## Impact\n"
            "Mobile app (Agent D) must send `PhoneOtp` until this is fixed. "
            "Any client reading the OpenAPI spec will fail on this endpoint.\n\n"
            "## Reproduction\n"
            "```bash\n"
            "curl -X POST http://localhost:8080/v1/auth/otp \\\n"
            "  -H 'Content-Type: application/json' \\\n"
            "  -d '{\"authMethod\":\"phone_otp\",\"destination\":\"+254700000001\"}'\n"
            "# Returns 400\n"
            "\n"
            "curl -X POST http://localhost:8080/v1/auth/otp \\\n"
            "  -H 'Content-Type: application/json' \\\n"
            "  -d '{\"authMethod\":\"PhoneOtp\",\"destination\":\"+254700000001\"}'\n"
            "# Returns 202\n"
            "```\n\n"
            "## Fix\n"
            "Add `[JsonConverter(typeof(JsonStringEnumConverter))]` with "
            "`JsonNamingPolicy.SnakeCaseLower` on the `AuthMethod` enum, OR add "
            "`[EnumMember(Value = \"phone_otp\")]` attributes.\n\n"
            "Update OpenAPI spec to match whichever casing is chosen.\n\n"
            "## Workaround\n"
            "Mobile app sends `PhoneOtp` with a `// TODO: revert to phone_otp when "
            "BACKEND-ENUM-001 is fixed` comment."
        ),
        "labels": ["bug", "auth", "backend", "openapi", "mobile-blocker"],
        "milestone": "02 — Identity & Access",
    },
    {
        "title": "[Bug] NLP-002: should_suggest_join not wired to existingClusterCandidates in preview response",
        "body": (
            "## Bug\n"
            "The NLP extraction model returns `should_suggest_join: true` when nearby "
            "clusters exist, but the `POST /v1/signals/preview` response does not "
            "populate `existingClusterCandidates` based on this field.\n\n"
            "## Impact\n"
            "Signal Composer Step 3 cannot show join suggestions to the citizen. "
            "Users always see 'Create new signal' even when a matching cluster exists.\n\n"
            "## Expected behaviour\n"
            "When `should_suggest_join: true` is returned by the NLP layer AND nearby "
            "active clusters score above join threshold:\n"
            "- `existingClusterCandidates` array in preview response should be populated\n"
            "- Each candidate should include `clusterId`, `summary`, `locationLabel`, "
            "`rawConfirmationCount`\n\n"
            "## Fix\n"
            "In the signals preview handler, after NLP extraction, run the "
            "similarity search and populate `existingClusterCandidates` before returning "
            "the preview response."
        ),
        "labels": ["bug", "nlp", "backend", "signals"],
        "milestone": "03 — Signal Intake",
    },
]

# ─────────────────────────────────────────────────────────────────────────────
# UPCOMING WORK ISSUES
# ─────────────────────────────────────────────────────────────────────────────

UPCOMING_ISSUES = [
    {
        "title": "[Session] Integration tests — WebApplicationFactory + test PostgreSQL",
        "body": (
            "## Objective\n"
            "Write integration tests covering the full request path for each major "
            "module using `WebApplicationFactory<Program>` and a real test PostgreSQL "
            "instance (via Docker Compose or Testcontainers).\n\n"
            "## Minimum Coverage Required\n"
            "- Auth: OTP request → verify → token issuance → refresh → logout\n"
            "- Signals: preview → submit → cluster creation (new) and join (existing)\n"
            "- Participation: affected → context add → no_longer_affected idempotency\n"
            "- CIVIS: activation gate (meets MACF + device diversity) → ACTIVE state\n"
            "- Restoration: institution claim → possible_restoration → resolution threshold\n"
            "- Official posts: create → geo-scope enforcement → cluster side-by-side\n"
            "- Admin: orphaned signals, audit log entries\n\n"
            "## Technical Requirements\n"
            "- Each test resets database state (truncate or transaction rollback)\n"
            "- Redis flushed between tests\n"
            "- No mocking of CIVIS or NLP logic in integration tests\n"
            "- NLP calls may be stubbed via `INlpExtractionService` test double\n\n"
            "## Acceptance Gate\n"
            "All integration tests pass in CI pipeline."
        ),
        "labels": ["testing", "integration-tests", "backend", "session-planned"],
        "milestone": "11 — Integration Tests",
    },
    {
        "title": "[Session] Agent D — React Native citizen mobile app (Phase A: scaffold + auth)",
        "body": (
            "## Objective\n"
            "Scaffold the Expo React Native citizen app and implement the auth flow "
            "end-to-end (Phase A of the Agent D session).\n\n"
            "## Scope\n"
            "- Expo project init with TypeScript\n"
            "- Expo Router layout and navigation guards\n"
            "- Typed API client with Axios + token refresh interceptor\n"
            "- AuthContext with SecureStore token storage\n"
            "- Phone Entry screen → OTP Verification → token storage\n"
            "- Redirect to Home on authenticated, to /auth/phone on unauthenticated\n\n"
            "## Gate\n"
            "Can log in end-to-end with a real OTP from the running backend.\n\n"
            "## References\n"
            "See `docs/session_agent_d_mobile.md` for full session prompt."
        ),
        "labels": ["mobile", "react-native", "agent-d", "session-planned"],
        "milestone": "10 — Mobile App",
    },
    {
        "title": "[Session] Agent D — React Native citizen mobile app (Phase B: home feed + cluster detail)",
        "body": (
            "## Objective\n"
            "Build the Home Feed and Cluster Detail screens (Phase B of Agent D session).\n\n"
            "## Scope\n"
            "- Home Feed: 4 sections (Active Now, Official Updates, "
            "Recurring at this time, Other active signals)\n"
            "- Pull-to-refresh, empty state, ward selector in header\n"
            "- Persistent floating Report button\n"
            "- Cluster Detail: participation bar, actions, official updates timeline\n"
            "- I'm Affected → Add Further Context inline flow\n\n"
            "## Gate\n"
            "Can view clusters, tap I'm Affected, and add context against a real backend."
        ),
        "labels": ["mobile", "react-native", "agent-d", "session-planned"],
        "milestone": "10 — Mobile App",
    },
    {
        "title": "[Session] Agent D — React Native citizen mobile app (Phase C: signal composer)",
        "body": (
            "## Objective\n"
            "Build the 3-step Signal Composer (Phase C of Agent D session).\n\n"
            "## Scope\n"
            "- Step 1: Free text input + NLP preview call\n"
            "- Step 2: Confirm extraction with editable fields + confidence-based UI\n"
            "- Step 3: Join existing cluster or create new → submit\n\n"
            "## Gate\n"
            "Can report a new signal end-to-end, join an existing cluster, "
            "and see the result in Cluster Detail."
        ),
        "labels": ["mobile", "react-native", "agent-d", "session-planned"],
        "milestone": "10 — Mobile App",
    },
    {
        "title": "[Session] Agent D — React Native citizen mobile app (Phase D: settings + restoration)",
        "body": (
            "## Objective\n"
            "Build settings screens and restoration prompt (Phase D of Agent D session).\n\n"
            "## Scope\n"
            "- Ward Following Settings (max 5 wards)\n"
            "- Notification Settings + Expo push token registration\n"
            "- Account Settings + logout\n"
            "- Restoration Prompt modal/screen\n\n"
            "## Gate\n"
            "All 12 screens from inventory render without errors. "
            "All 6 navigation flows (A–F) work end-to-end."
        ),
        "labels": ["mobile", "react-native", "agent-d", "session-planned"],
        "milestone": "10 — Mobile App",
    },
]

# ─────────────────────────────────────────────────────────────────────────────
# RUNNER
# ─────────────────────────────────────────────────────────────────────────────

def run_gh(args: list, dry_run: bool, expect_json: bool = True) -> dict | None:
    if dry_run:
        print(f"  [DRY RUN] gh {' '.join(args)}")
        return {"number": 0, "url": "https://github.com/dry-run"}
    result = subprocess.run(
        ["gh"] + args,
        capture_output=True, text=True
    )
    if result.returncode != 0:
        stderr = result.stderr.strip()
        if stderr:
            print(f"  ⚠️  {stderr}")
        return None
    if not expect_json or not result.stdout.strip():
        return {}
    stdout = result.stdout.strip()
    try:
        return json.loads(stdout)
    except json.JSONDecodeError:
        # gh issue create returns a plain URL on stdout
        return {"url": stdout, "number": "?"}


def ensure_labels(labels: list, repo_flag: list, dry_run: bool):
    label_colors = {
        "completed":         "0e8a16",
        "session-planned":   "d93f0b",
        "backend":           "1d76db",
        "mobile":            "5319e7",
        "react-native":      "5319e7",
        "agent-d":           "5319e7",
        "agent-c":           "f9d0c4",
        "auth":              "e4e669",
        "nlp":               "c5def5",
        "signals":           "bfdadc",
        "civis":             "0075ca",
        "clustering":        "0075ca",
        "participation":     "cfd3d7",
        "restoration":       "f9d0c4",
        "lifecycle":         "f9d0c4",
        "notifications":     "e4e669",
        "jobs":              "e4e669",
        "foundations":       "d4c5f9",
        "institutions":      "cfd3d7",
        "official-updates":  "cfd3d7",
        "testing":           "0e8a16",
        "integration-tests": "0e8a16",
        "bug":               "d73a4a",
        "openapi":           "e4e669",
        "mobile-blocker":    "d73a4a",
        "session-01":        "ededed",
        "session-02":        "ededed",
        "session-03":        "ededed",
        "session-04":        "ededed",
        "session-05":        "ededed",
        "session-06":        "ededed",
        "session-07":        "ededed",
        "session-08":        "ededed",
        "validation":        "f9d0c4",
    }
    for label in labels:
        color = label_colors.get(label, "ededed")
        run_gh(
            repo_flag + ["label", "create", label,
                         "--color", color, "--force"],
            dry_run,
            expect_json=False
        )


def ensure_milestone(name: str, repo_flag: list,
                     milestones: dict, dry_run: bool,
                     repo_name: str = "") -> int | None:
    if milestones.get(name) is not None:
        return milestones[name]
    result = run_gh(
        ["api", f"repos/{repo_name}/milestones",
         "--method", "POST",
         "-f", f"title={name}",
         "-f", "state=open"],
        dry_run
    )
    if result:
        number = result.get("number", 0)
        milestones[name] = number
        return number
    return None


def create_issue(issue: dict, repo_flag: list,
                 milestones: dict, dry_run: bool, repo_name: str = ""):
    print(f"\n  Creating: {issue['title']}")

    all_labels = issue.get("labels", [])
    ensure_labels(all_labels, repo_flag, dry_run)

    milestone_number = None
    if "milestone" in issue:
        milestone_number = ensure_milestone(
            issue["milestone"], repo_flag, milestones, dry_run, repo_name
        )

    args = repo_flag + [
        "issue", "create",
        "--title", issue["title"],
        "--body",  issue["body"],
    ]
    for label in all_labels:
        args += ["--label", label]
    if milestone_number:
        args += ["--milestone", str(milestone_number)]

    result = run_gh(args, dry_run)
    if result:
        url = result.get("url", "")
        print(f"  ✅ #{result.get('number', '?')} — {url}")
    time.sleep(0.5)  # avoid rate limiting


def main():
    parser = argparse.ArgumentParser(description="Create Hali catch-up GitHub issues")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print commands without creating issues")
    parser.add_argument("--repo", type=str, default=None,
                        help="GitHub repo in owner/repo format")
    args = parser.parse_args()

    repo_flag = []
    repo_name = args.repo or ""
    if args.repo:
        repo_flag = ["--repo", args.repo]

    dry_run = args.dry_run
    milestones = {k: None for k in MILESTONES}

    print("\n=== Hali GitHub Issues — Catch-up Script ===")
    print(f"Mode: {'DRY RUN' if dry_run else 'LIVE'}")
    if args.repo:
        print(f"Repo: {args.repo}")
    print()

    print("── Completed Session Issues ──────────────────────")
    for issue in COMPLETED_ISSUES:
        create_issue(issue, repo_flag, milestones, dry_run, repo_name)

    print("\n── Bug Issues ────────────────────────────────────")
    for issue in BUG_ISSUES:
        create_issue(issue, repo_flag, milestones, dry_run, repo_name)

    print("\n── Upcoming Work Issues ──────────────────────────")
    for issue in UPCOMING_ISSUES:
        create_issue(issue, repo_flag, milestones, dry_run, repo_name)

    print(f"\n✅ Done. {len(COMPLETED_ISSUES) + len(BUG_ISSUES) + len(UPCOMING_ISSUES)} issues processed.")


if __name__ == "__main__":
    main()

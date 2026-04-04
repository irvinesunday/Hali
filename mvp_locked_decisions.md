# Hali MVP Locked Decisions (Implementation Phase, v2)

## Purpose

This document records the implementation decisions that are now locked for Phase 1 MVP.

---

## 1. Runtime NLP Strategy
Decision:
Use Anthropic Claude Sonnet as the MVP CSI-NLP extraction engine.

Rules:
- JSON output only
- backend validates against taxonomy
- no separate Python NLP microservice in MVP

Interface:
- INlpExtractionService

Preview response shape:
- `POST /v1/signals/preview` returns `candidates[]` (structured extractions)
  and `existingClusterCandidates[]` (nearby clusters the signal could join).
- Each existing cluster candidate: `clusterId`, `summary`, `locationLabel`, `rawConfirmationCount`
- The earlier design (`shouldSuggestJoin: bool`) is superseded by this array-based approach.

---

## 2. Mobile App Scope
Decision:
Phase 1 MVP includes the citizen mobile app only.
No admin portal or institution dashboard in this phase.

Required screen inventory:
See mobile_screen_inventory.md (project root)

---

## 3. Auth / OTP Provider
Decision:
Use Africa’s Talking SMS API for OTP delivery.

Rules:
- server generates OTP
- server verifies OTP
- provider wrapped behind abstraction
- `OtpRequestDto.AuthMethod` typed as `AuthMethod` enum, accepting `phone_otp` / `email_otp` / `magic_link`
  via `JsonStringEnumConverter(SnakeCaseLower)` — backend normalises snake_case → PascalCase before parse
- `/v1/auth/verify` is the canonical verify route (not `/v1/auth/verify-otp`)

Interface:
- ISmsProvider

### Institution Auth
Decision:
Invite-based institution onboarding under admin oversight.

Flow:
1. Admin creates institution via `POST /v1/admin/institutions` → returns invite link
2. Institution rep opens link, registers phone via `POST /v1/auth/institution/setup` → OTP sent
3. Rep verifies via standard `POST /v1/auth/verify` → JWT with `role: institution`, `institution_id`
4. Admin can revoke via `DELETE /v1/admin/institutions/{id}/access` → blocks accounts, revokes tokens

Tables:
- `institution_invites` (invite_token_hash, institution_id, expires_at, accepted_at)
- `accounts.institution_id` (nullable FK), `accounts.is_blocked`

Roles in JWT:
- `citizen` (default), `institution`, `admin`
- `institution_id` claim present only for institution accounts

---

## 4. Auth Token Strategy
Decision:
Use access token + refresh token for MVP.

Rules:
- issue refresh token at OTP verification
- refresh token expiry = 30 days
- persist refresh tokens server-side as hashes
- refresh tokens tied to account_id + device_id
- add `POST /v1/auth/refresh`

Refresh token persistence shape:
- token_hash
- account_id
- device_id
- expires_at
- revoked_at
- created_at

---

## 5. Spatial Cell System
Decision:
Use H3 for spatial_cell_id.

Locked MVP value:
- H3 resolution = 9

Rules:
- store raw lat/lng separately
- H3 is candidate bucketing, not final merge logic
- exact distance checks still required

Library:
- H3.net

---

## 6. Geocoding Strategy
Decision:
Use Nominatim / OpenStreetMap for MVP geocoding and reverse geocoding.

Rules:
- use IGeocodingService
- add caching for repeated reverse geocoding
- do not leak vendor response shapes into domain logic

---

## 7. Push Notification Strategy
Decision:
Use Expo Notifications with Expo Push Tokens.

Rules:
- store push token per device
- backend sends via Expo Push API
- no direct FCM/APNs in MVP

Interface:
- IPushNotificationService

---

## 8. Schema Management Strategy
Decision:
Use EF Core code-first migrations as the source of truth for schema management.

Rules:
- SQL DDL files are reference artifacts
- do not operate schema evolution by manually applying raw SQL files
- each module owns its own migration set

---

## 9. CIVIS Starter Constants

### Shared Constants
- min_unique_devices_for_activation = 2
- join_threshold = 0.65
- restoration_ratio = 0.60
- min_restoration_affected_votes = 2
- context_edit_window_minutes = 2
- wrab_rolling_window_days = 30

### Category Starter Values

| Category | base_floor | half_life_hours | macf_min | macf_max |
|---|---:|---:|---:|---:|
| roads | 2 | 18 | 2 | 6 |
| transport | 2 | 8 | 2 | 5 |
| electricity | 2 | 12 | 2 | 6 |
| water | 2 | 24 | 2 | 7 |
| environment | 2 | 36 | 2 | 6 |
| safety | 2 | 18 | 2 | 6 |
| infrastructure | 2 | 24 | 2 | 6 |

### CIVIS Formulas
Decay constant:
lambda = ln(2) / half_life_hours

WRAB:
WRAB = Σ(weighted_active_signal_events over rolling window)
effective_WRAB = max(WRAB, base_floor)

SDS:
SDS = current_active_signal_pressure / effective_WRAB

MACF:
MACF = clamp(ceil(base_floor + log2(SDS + 1)), macf_min, macf_max)

Join score:
join_score = 0.40 * category_match + 0.25 * distance_score + 0.20 * time_score + 0.15 * condition_match

Join existing cluster if:
join_score >= 0.65

Restoration resolution:
- affected_restore_votes / total_affected_votes >= 0.60
- and affected_restore_votes >= 2

---

## 10. Screen Flow Decision
Decision:
Use the 3-step composer flow:
1. free text input
2. confirm extraction
3. join existing or create new

---

## 11. Phase 1 Backend Service Interfaces
- INlpExtractionService
- ISmsProvider
- IGeocodingService
- IPushNotificationService

---

## 12. Phase 1 Infra Checklist
- PostgreSQL + PostGIS
- Redis
- Anthropic API key
- Africa’s Talking credentials
- Expo push credentials
- Nominatim config
- JWT config
- refresh token expiry config
- Dockerized local development

See `.env.example` in the project root.

---

## Final Reminder
These decisions are locked to reduce ambiguity during implementation.
Do not casually change them during coding.


### CIVIS Decay Constants (documented post-build, Agent C BLOCKING-6)

| Constant | Value | Description |
|---|---|---|
| `DeactivationThreshold` | 0.5 | Ratio of `live_mass / effective_wrab` below which a cluster decays. Active → PossibleRestoration; PossibleRestoration → Resolved. Configurable via `CIVIS_DEACTIVATION_THRESHOLD`. |

This threshold is separate from the restoration vote threshold. Decay-based resolution
fires when activity falls below 50% of the local baseline, regardless of citizen votes.
Must be lower than the activation threshold to prevent cluster state flicker.

---

## 13. Home Feed Pagination

### Per-Section Limits (Locked)
| Section | Max items per page |
|---|---:|
| `active_now` | 20 |
| `official_updates` | 5 |
| `recurring_at_this_time` | 10 |
| `other_active_signals` | 10 |

### Pagination
- Cursor-based: each section returns `nextCursor` (opaque base64 token); null = no more pages
- `GET /v1/home?section=active_now&cursor=<token>` fetches a specific section page
- First page (no cursor) cached in Redis per locality set, TTL 30 seconds
- Subsequent pages fetched live (no cache)

---

## 14. Official Posts — Jurisdiction Check

- Jurisdiction check runs BEFORE DB insert — no out-of-scope post can ever
  exist in the database even transiently
- `CreateOfficialPostRequestDto` uses `OfficialPostType` field (matching OpenAPI spec enum)
- Official post endpoints require `[Authorize(Roles = "institution")]`
- `institution_id` JWT claim flows into the jurisdiction check end-to-end

---

## 15. Outbox Relay Worker

- Polling interval: every 5 seconds
- Up to 100 events per cycle
- Health indicator: rows older than 60 seconds trigger warning log
- Relay registered as hosted service in `Program.cs`

---

## Post-MVP Backlog (intentionally deferred)

- Institution 2FA hardening (mandatory second factor for institution accounts)
- Admin dashboard UI
- Map-based browsing (react-native-maps)
- Media / photo uploads on signals
- Ward-level analytics dashboard for institutions
- Dredd contract test integration (ops/slos session)
- SLO alert wiring to Grafana / PagerDuty

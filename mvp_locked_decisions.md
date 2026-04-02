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

Interface:
- ISmsProvider

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

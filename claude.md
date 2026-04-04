# Hali MVP – Claude Engineering Guide (Phase 1 Locked, Implementation Update v2)

## Project Identity

Hali is a civic communication convergence system — a neutral “civic weather system” that helps people understand what is happening in a locality through structured citizen signals and official updates.

Hali is NOT:
- a social network
- a complaint forum
- a political platform
- a ranking system

It is infrastructure for civic visibility and coordination.

---

## Phase 1 MVP Scope (STRICT)

This phase includes ONLY:

### In Scope
- Citizen mobile app (React Native + Expo)
- Backend API (.NET)
- Core civic signal system:
  - Signal creation (NLP-first)
  - Signal clustering
  - Participation (“I’m Affected” / “I’m Observing”)
  - CIVIS activation logic
  - Signal lifecycle (Unconfirmed → Active → Possible Restoration → Resolved)
- Official updates (basic support)
- Location resolution
- Ward following (max 5)
- Basic notifications
- Core infrastructure (PostgreSQL + Redis)

### Out of Scope (DO NOT BUILD)
- Admin portal
- Institution dashboards
- Comments / replies
- Likes / reactions
- Messaging / chat
- Media uploads
- Social profiles / followers
- Gamification
- Ranking systems
- Advanced analytics dashboards
- AI features beyond CSI-NLP extraction
- Multi-provider auth abstractions beyond what is needed for SMS OTP
- Microservices
- Complex real-time sockets unless explicitly needed later

If any of these appear necessary:
→ STOP and ask before implementing.

---

## Technology Stack (MANDATORY)

### Backend
- Language: C#
- Framework: .NET (ASP.NET Core Web API)
- Architecture: Modular Monolith

### Database
- PostgreSQL

### Spatial
- PostGIS extension enabled in PostgreSQL
- H3 for spatial_cell_id
- Use H3 resolution 9 for MVP
- Use H3.net library

### Caching / Background Jobs
- Redis
- .NET Hosted Services / Workers for background processing

### Citizen Frontend
- React Native (Expo)
- TypeScript
- Use Expo Router (file-based routing). Do not use React Navigation.

### API Style
- REST (OpenAPI aligned)
- Use `/v1/*` versioned routes

### Data Access
- Entity Framework Core

### SMS / OTP
- Africa’s Talking SMS API

### CSI-NLP
- Anthropic Claude API (Claude Sonnet)
- Structured JSON extraction only

### Push Notifications
- Expo Notifications
- Expo Push Tokens stored per device
- Backend uses Expo Push API

### Geocoding
- Nominatim / OpenStreetMap for MVP
- Must be wrapped behind an interface so it can be swapped later

### Local Development
- Docker + Docker Compose

---

## Stack Rules

- Do NOT introduce Node.js or Python as backend services
- Do NOT introduce microservices
- Do NOT replace PostgreSQL
- Do NOT replace H3 with geohash or custom spatial bucketing
- Do NOT introduce Firebase as the primary backend
- Do NOT build direct FCM/APNs integration in MVP
- Do NOT build a separate NLP microservice for MVP
- Python is only allowed later for offline experimentation, not for MVP runtime backend

---

## Auth Rules

- Use Africa’s Talking SMS API for OTP delivery
- `OtpRequestDto.AuthMethod` is typed as `AuthMethod` enum (not raw string).
  Accepts `phone_otp`, `email_otp`, `magic_link` via `JsonStringEnumConverter(SnakeCaseLower)`.
  Backend normalises snake_case → PascalCase before enum parse.
- OTP verification issues:
  - short-lived access token (default 60 min)
  - 30-day refresh token
- `POST /v1/auth/verify` is the canonical verify route (not `/v1/auth/verify-otp`)
- `POST /v1/auth/refresh` exchanges a refresh token for a new token pair
- Refresh tokens must be stored server-side as hashes, not plaintext
- Persist refresh token records with:
  - token_hash
  - account_id
  - device_id
  - expires_at
  - revoked_at
  - created_at
- Refresh token rotation: old token revoked on each refresh, new pair issued
- Add `REFRESH_TOKEN_EXPIRY_DAYS=30` to environment configuration

### Institution Auth (Invite-Based Flow)

Institutions authenticate via a controlled onboarding path:
1. Admin creates an institution via `POST /v1/admin/institutions` (requires `role: admin` JWT)
2. System generates a one-time setup link (Base64 token, SHA-256 hash stored, 72-hour expiry)
3. Institution representative opens link, registers phone via `POST /v1/auth/institution/setup`
4. System sends OTP to that phone; representative verifies via standard `POST /v1/auth/verify`
5. JWT issued with `role: institution` and `institution_id: <uuid>` claims

This keeps a single auth mechanism (OTP) while giving institutions a controlled path.

Admin can revoke all institution access via `DELETE /v1/admin/institutions/{id}/access`,
which blocks all linked accounts and revokes their active refresh tokens.
Institution posts remain visible (history preserved per neutrality doctrine).

---

## Migration Rules

Use EF Core code-first migrations.
The provided SQL DDL in `01_postgres_schema.sql` is the reference, but migrations are the source of truth for schema management.
Each module owns its own migration set.
Do not apply the raw SQL file directly as the operational migration system.

---

## Core Product Doctrine

### 1. Neutrality
- No blame
- No ranking
- No arbitration

### 2. Dual Visibility
- Citizen signals and official updates coexist
- Neither overrides the other

### 3. Signals, Not Speech
- No comments
- No discussion threads
- No civic “wall”
- Structured civic input only

### 4. Participation Over Posting
Primary actions:
- “I’m Affected”
- “I’m Observing”

### 5. Resolution Requires Confirmation
- Signals do NOT auto-resolve from official updates alone
- Citizen confirmation is required for final resolution

### 6. Cluster-First Public Reality
- Raw contributions are stored as SignalEvents
- Public-facing civic reality is represented by SignalClusters
- Do NOT make the public feed raw event spam

---

## UX Principles

### List-Led Experience
- Home is list-first, not map-first

### Calm Civic Tone
- Neutral, factual, non-alarmist language

### Location Clarity
Use:
- road + landmark + area, where applicable
- area-level only for experiential utility issues when road-level specificity is not appropriate

### Report Button
- Persistent report button is available throughout the app
- It does not become a social “post” action

---

## Architecture (Modular Monolith)

src/
- Hali.Api
- Hali.Application
- Hali.Domain
- Hali.Infrastructure
- Hali.Workers
- Hali.Contracts

tests/
scripts/
docs/

---

## Domain Concepts

- SignalEvent → raw user contribution
- SignalCluster → public representation of a civic condition
- Participation → “Affected” / “Observing” state on a cluster
- OfficialUpdate → structured institution-originated update
- CIVIS → internal trust and validation engine
- CSI-NLP → free text to structured candidate signal extraction
- TDA → time behavior and recurrence classification
- Device → installation/device integrity anchor
- WardFollow → user followed ward relationship
- RefreshToken → server-side persisted hashed refresh token record tied to account + device

---

## Locked Implementation Decisions

### CSI-NLP MVP Decision
Use the Anthropic Claude API (Claude Sonnet) as the MVP extraction engine.
The NLP layer must return structured JSON only.
Do not introduce a separate Python NLP service.
All extraction outputs must be validated against the canonical Hali taxonomy before persistence.

### Auth Provider
Use Africa’s Talking SMS API for OTP delivery.
Implement the provider behind an abstraction interface.
Do not build a custom telecom integration.

### Spatial Indexing
Use H3 for spatial_cell_id.
Use H3 resolution 9 for MVP.
Use H3 as a candidate-bucketing/indexing mechanism, then apply exact clustering rules using distance, time, and category compatibility.
Store latitude and longitude separately in addition to spatial_cell_id.

### Push Notifications
Use Expo Notifications for the React Native MVP.
Use Expo Push Tokens for device registration and Expo Push API for backend delivery.
Do not build direct FCM/APNs integration in MVP.

### Geocoding
Use Nominatim / OpenStreetMap for MVP behind an IGeocodingService interface.
Cache reverse geocoding results.
Do not couple domain logic to vendor-specific response shapes.

---

## Document Authority

When documents in this pack conflict, the following hierarchy applies:

1. `claude.md` (this file) — implementation authority
2. `mvp_locked_decisions.md` — locked decisions authority
3. `mobile_screen_inventory.md` — frontend authority
4. `openapi_patch_checklist.md` — API contract authority
5. `01_postgres_schema.sql` — schema reference

**The following files are reference context only — not implementation authority:**
- `Claude.md` (uppercase — old version, superseded by this file)
- `Claude_Seed_Prompt.md` (old paths, superseded by claude.md)
- `Hali_MVP_Claude_Execution_Pack.docx` (old paths, superseded by claude.md)
- `09_claude.md` (earlier version, superseded by this file)
- `10_repository_structure.md` (proposes a different module-per-domain layout; superseded by Folder_Structure.md and the Architecture section of this file)

All spec files (`mvp_locked_decisions.md`, `mobile_screen_inventory.md`, `nlp_extraction_prompt.md`,
`openapi_patch_checklist.md`, `schema_patch_notes.md`, `seed_taxonomy_expanded.sql`) live at
the **project root**, not in a `docs/` subfolder.

Where any older files conflict with claude.md or locked_decisions, **claude.md wins**.

---

## Working Protocol

Before writing any code, read these files in this order:

1. `claude.md` — this file (you are reading it)
2. `mvp_locked_decisions.md` — all locked implementation decisions
3. `01_postgres_schema.sql` — canonical schema DDL
4. `mobile_screen_inventory.md` — before any frontend work
5. `nlp_extraction_prompt.md` — before NLP integration (Phase 5)
6. `openapi_patch_checklist.md` — before implementing any endpoint
7. `schema_patch_notes.md` — before generating EF Core migrations
8. `seed_taxonomy_expanded.sql` — before seeding the database
9. `.env.example` — before configuring any service

When uncertain about a decision: stop and ask. Do not guess and proceed.
Show structure before implementation at each phase.
Call out assumptions explicitly before acting on them.

---

## Build Sequence (MANDATORY — do not skip ahead)

Claude Code must build in this order. Do not start a phase until the prior phase is
committed and confirmed working. Show structure before writing implementation code.

1. **Repo structure** — scaffold solution, projects, module folders, test projects
2. **Core schema** — EF Core migrations per module derived from 01_postgres_schema.sql
3. **Auth** — OTP flow, JWT issuance, refresh token store, device registration
4. **Signal ingestion** — SignalEvent persistence, idempotency, rate limiting
5. **CSI-NLP integration** — INlpExtractionService, prompt call, DTO mapping, validation
6. **Geocoding integration** — IGeocodingService, Nominatim, reverse geocoding cache
7. **Clustering** — H3 candidate search, join-score, cluster create/join, outbox emit
8. **CIVIS logic** — WRAB, SDS, MACF, activation gate, decay worker, civis_decisions
9. **Participation** — affected/observing/no_longer_affected, one-type-per-device rule
10. **Official updates** — institution posts, geo-scope enforcement, dual-visibility
11. **Restoration** — restoration-response endpoint, threshold evaluation, state transition
12. **Notifications** — push token registration, Expo Push API delivery, restoration prompts

If any phase reveals a conflict with earlier phases, stop and surface it rather than
patching forward silently.

---

## API Path Authority

Treat the OpenAPI spec (patched per `openapi_patch_checklist.md`) as the authority for route naming and versioning.
Frontend screen inventory must match the OpenAPI spec exactly.

Implemented endpoints (source of truth — matches running code):

**Auth:**
- `POST /v1/auth/otp` — request OTP
- `POST /v1/auth/verify` — verify OTP, issue JWT + refresh token
- `POST /v1/auth/refresh` — exchange refresh token for new pair
- `POST /v1/auth/logout` — revoke refresh token
- `POST /v1/auth/institution/setup` — accept invite, register phone, send OTP

**Home & Feed:**
- `GET /v1/home` — paginated home feed (`section`, `cursor` query params)

**Signals:**
- `POST /v1/signals/preview` — NLP extraction + existing cluster candidates
- `POST /v1/signals/submit` — submit confirmed signal event

**Clusters:**
- `GET /v1/clusters/{id}` — cluster detail with official updates
- `POST /v1/clusters/{id}/participation` — affected / observing / no_longer_affected
- `POST /v1/clusters/{id}/context` — add further context (requires affected)
- `POST /v1/clusters/{id}/restoration-response` — restoration vote

**Localities:**
- `GET /v1/localities/followed` — list followed wards
- `PUT /v1/localities/followed` — set followed wards (max 5)

**Official Posts:**
- `POST /v1/official-posts` — create geo-scoped institution post (role=institution)

**Users & Devices:**
- `GET /v1/users/me` — current user profile
- `PUT /v1/users/me/notification-settings` — update notification prefs
- `POST /v1/devices/push-token` — register/update expo push token

**Admin:**
- `POST /v1/admin/institutions` — create institution + issue invite (role=admin)
- `DELETE /v1/admin/institutions/{id}/access` — revoke institution access (role=admin)

---

## Application-Layer Business Rules

The following rules must be enforced in service code, not only database constraints.

### Participation — one type per device per cluster
A device may only hold one active participation type per cluster at a time.
Before inserting a new participation row, the service must cancel or replace any
existing participation row for that (cluster_id, device_id) pair.
The DB constraint alone does not enforce this.

### Ward following — max 5 per account
The followed-wards service must reject a PUT /v1/localities/followed request that
would result in more than 5 followed localities for the account.
Return HTTP 422 with code `max_followed_localities_exceeded`.

### Add Further Context — only after "I'm Affected"
POST /v1/clusters/{id}/context must be rejected if the requesting device does not
have an active `affected` participation record on that cluster.
Return HTTP 422 with code `context_requires_affected_participation`.

### Context edit window
Context edits are only accepted within CIVIS_CONTEXT_EDIT_WINDOW_MINUTES of the
original affected participation. After the window, return HTTP 422 with code
`context_edit_window_expired`.

---

## Copilot Review Comments

When resolving GitHub Copilot review comments on pull requests, follow the workflow defined in
`docs/arch/COPILOT_RESOLUTION_SKILL.md`. Key rules:

- Always read the current file at HEAD before applying a fix — comment line numbers may be stale
- Search the full PR diff for other instances of the same problem before committing
- Never apply a fix that conflicts with locked decisions (`claude.md`, `mvp_locked_decisions.md`)
- Fix all instances of a pattern bug, not just the one the comment points to

---

## Final Reminder

Hali is not a social app.

Build it as a calm, trustworthy civic infrastructure system that helps people understand and navigate real-world conditions.

---

## PR Description Rules (MANDATORY)

When creating a pull request via `gh pr create` or any GitHub API call, you MUST populate the PR body fully. Never submit a PR with empty template fields.

Before creating any PR, construct the full body by filling in every section of the template:

```
## Summary
One or two sentences describing what this PR does and why.

## Session / Phase
Session: [e.g. Session 04 — Clustering & CIVIS]
Phase: [e.g. Phase 1 — Citizen Mobile]

## Agent C Verdict
Verdict: [PASS / PASS_WITH_NOTES / FAIL — or PENDING if not yet run]
Approved to merge: [Yes / No / Pending]

## Coverage
Line coverage: [e.g. 91.4% — or PENDING if CI hasn't run]
Gate status: [PASS / INFORMATIONAL / PENDING]

## Changes made
- [Bullet 1: specific thing implemented]
- [Bullet 2: specific thing implemented]

## How to test
1. [Step 1]
2. [Step 2]

## Checklist
- [ ] All 6 CI jobs are green
- [ ] Coverage gate >= 95%
- [ ] Agent C validation report reviewed
- [ ] No hardcoded secrets or API keys
- [ ] No .env files committed
- [ ] EF Core migrations are reversible
- [ ] Outbox events written in same transaction as state changes
- [ ] No features outside MVP scope introduced
```

Pass this entire string as the `--body` argument when creating the PR.
Do NOT use `--body-file` pointing to the raw template — always construct the populated version inline.
If Agent C has not run yet, mark Verdict and coverage as PENDING rather than leaving blank.

---

## Branching Strategy

- Base branch for all feature/fix/chore branches: `develop`
- All PRs from feature branches must target `develop`, never `main`
- `main` is updated only via a release PR from `develop`
- When creating a branch, always branch off the latest `develop`:
  `git checkout develop && git pull && git checkout -b <branch-name>`
- Never use `--base main` in `gh pr create`

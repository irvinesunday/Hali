# Hali — Claude Code Master Brief
**This file is read automatically at every session start. It is your complete operating contract.**

There are two things this file does:
1. **Architecture authority** — what to build, how it works, what the rules are
2. **Session execution** — how to run the build, session sequence, gates, agent model

Read both parts. Neither overrides the other.

---

# PART 1 — ARCHITECTURE AUTHORITY

---

## What Hali is

Hali is a neutral civic signal platform — a "civic weather system" that aggregates citizen observations and official institution updates into structured, real-time locality insights. It is civic infrastructure, not a social network.

You are implementing a production-grade modular monolith. The product doctrine is frozen. Do not invent features. Do not simplify CIVIS logic. When uncertain, stop and ask.

---

## Platform surfaces and phases

| Surface | Stack | Phase |
|---|---|---|
| Citizen Mobile App | React Native + Expo + TypeScript | Phase 1 |
| Institution Operations Dashboard | Next.js + TypeScript + Tailwind + shadcn/ui | Phase 2 |
| Institution Admin Dashboard | Next.js + TypeScript + Tailwind + shadcn/ui | Phase 2 |
| Hali Ops Admin Dashboard | Next.js + TypeScript + Tailwind + shadcn/ui | Phase 3 |
| Backend API | C# ASP.NET Core, Modular Monolith | All phases |
| Background Workers | C# .NET Workers | All phases |

**Phase 1 is the entire scope of sessions 00–07. Do not build Phase 2 or 3 surfaces during Phase 1.**

---

## Phase 1 scope (STRICT)

### In scope
- Citizen mobile app (React Native + Expo)
- Backend API (.NET 10) + background workers
- Signal creation (NLP-first via CSI-NLP)
- Signal clustering + CIVIS activation logic
- Participation (I'm Affected / I'm Observing)
- Signal lifecycle: Unconfirmed → Active → Possible Restoration → Resolved
- Official updates (Live Update, Scheduled Disruption, Advisory/Public Notice)
- Ward following (max 5 per account)
- Push notifications (restoration prompts, new clusters in followed wards)
- Core infrastructure (PostgreSQL + PostGIS + Redis)

### Out of scope — DO NOT BUILD
- Admin portal, institution dashboards (Phase 2–3)
- Comments, replies, discussion threads of any kind
- Likes, reactions, or any engagement mechanics
- Messaging or chat
- Media uploads
- Social profiles or follower graphs
- Gamification or rankings
- Microservices
- Anything not in the in-scope list

If any of these seem necessary: stop and ask before building.

---

## Monorepo structure

```
/hali
  /apps
    /citizen-mobile            # React Native + Expo (Phase 1)
    /institution-web           # Phase 2
    /institution-admin-web     # Phase 2
    /hali-ops-web              # Phase 3
    /api                       # ASP.NET Core Web API
    /workers                   # .NET Background Workers

  /packages
    /contracts                 # Framework-agnostic TypeScript types (all surfaces)
    /taxonomy                  # Category/subcategory/condition constants (all surfaces)
    /config                    # Non-secret app constants (all surfaces)
    /design-system             # Tailwind + shadcn/ui — WEB ONLY, never in citizen-mobile

  /src                         # C# modular monolith
    /Hali.Api
    /Hali.Workers
    /Hali.Domain
    /Hali.Application
    /Hali.Infrastructure
    /Hali.Contracts
    /Hali.Modules.Auth
    /Hali.Modules.Signals
    /Hali.Modules.Clusters
    /Hali.Modules.Participation
    /Hali.Modules.Advisories
    /Hali.Modules.Institutions
    /Hali.Modules.Admin
    /Hali.Modules.Civis
    /Hali.Modules.Nlp
    /Hali.Modules.Notifications
    /Hali.Modules.Metrics

  /tests
  /docs/arch       # Architecture reference — read before each implementation area
  /scripts
  /agent_prompts
  /session_prompts
  /agent_outputs
  .github/workflows
```

---

## Technology stack (mandatory — do not change)

### Backend
- Language: C# / .NET 10
- Framework: ASP.NET Core Web API
- Architecture: Modular monolith
- ORM: Entity Framework Core (code-first migrations = schema source of truth)
- Database: PostgreSQL 16 + PostGIS
- Spatial: H3.net at resolution 9
- Cache / Queue: Redis 7
- NLP: Anthropic Claude API (Claude Sonnet), structured JSON only
- SMS / OTP: Africa's Talking via ISmsProvider
- Push: Expo Push API via IPushNotificationService
- Geocoding: Nominatim / OpenStreetMap via IGeocodingService

### Citizen frontend
- React Native + Expo (latest stable SDK)
- TypeScript strict mode
- Expo Router (file-based routing)
- Expo SecureStore (token storage — not AsyncStorage)

### Monorepo
- pnpm workspaces + Turborepo

---

## Stack rules (violations block merge)

- Do NOT introduce Node.js or Python as backend runtime
- Do NOT introduce microservices
- Do NOT replace PostgreSQL or Redis
- Do NOT replace H3 with geohash or custom spatial bucketing
- Do NOT build direct FCM/APNs — use Expo Push
- Do NOT build a separate NLP microservice
- Do NOT import /packages/design-system into citizen-mobile
- Do NOT use localStorage for tokens on web surfaces (httpOnly cookies)

---

## Auth rules

- Citizens: SMS OTP via Africa's Talking → JWT (60 min) + refresh token (30 days)
- Refresh tokens stored as SHA-256 hashes — never plaintext
- Refresh token record shape: token_hash, account_id, device_id, expires_at, revoked_at, replaced_by_token_hash
- Token rotation is required. replaced_by_token_hash enables theft detection: re-presenting a rotated token revokes the entire token family
- JWT audience: hali-platform (not hali-mobile)
- Phase 2 web surfaces: email magic link + mandatory TOTP 2FA + httpOnly cookies

---

## Migration rules

- EF Core code-first migrations are the schema source of truth
- Reference DDL is in docs/arch/01_schema_reference.md — do not apply it directly
- Each module owns its own migration set
- Do NOT replicate broken constraints from the original 01_postgres_schema.sql
  The corrected versions are in docs/arch/01_schema_reference.md

---

## Non-negotiable product doctrine

1. **Neutrality** — No rankings, no blame, no political interpretation.
2. **Dual visibility** — Citizen signals and official updates coexist. Neither overrides the other.
3. **Cluster-first public reality** — Raw SignalEvent rows are internal. Public sees SignalCluster objects only.
4. **Signals, not speech** — No comments or threads. Structured civic input only.
5. **Anonymous participation** — Citizen identity never exposed on public surfaces.
6. **CIVIS is internal** — civis_score, wrab, sds, macf, reason codes never leave trusted surfaces.
7. **Restoration requires confirmation** — Official claims → possible_restoration only. Citizen threshold (≥60%, ≥2 affected votes) required for resolved.
8. **NLP-first** — Text in, structured candidate out, user confirms before submit.
9. **Join over create** — Prefer joining existing clusters over creating duplicates.

---

## UX rules

- Home is list-first — no map in Phase 1
- Calm state shows "Currently calm in [ward name]" with timestamp — not a generic empty illustration
- Persistent Report FAB throughout the app
- Add Further Context only appears after I'm Affected, only within 2-minute window
- Offline: write paths queue locally, read paths serve cached response with freshness timestamp

---

## Engineering rules

- Contract first — update OpenAPI before diverging frontend/backend
- UTC everywhere — stored and transmitted UTC, rendered in local time client-side
- Outbox pattern — every state-changing write emits an outbox_events row in the same transaction
- Idempotency — all mutation endpoints require `idempotencyKey` in the request body; do not use an `Idempotency-Key` header; all workers are replay-safe
- Audit trail — every privileged action writes structured log + audit entry
- No magic state changes — every cluster transition emits a domain event with a reason code
- Server-side enforcement — all role/scope checks are server-side; frontend hiding is not security

---

## Layering rules

```
Domain     → civic vocabulary, invariants, pure domain services. No HTTP, no DB, no queue.
Application → use cases, commands, queries, DTOs. No UI policy, no raw SQL.
Infrastructure → EF Core, Redis, adapters, external integrations.
API/Workers → composition roots. Wire dependencies here only.
```

---

## Domain concepts glossary

| Term | Meaning |
|---|---|
| SignalEvent | Raw user contribution — single report from a single device |
| SignalCluster | Public civic condition object — aggregates signal events |
| Participation | Affected / Observing / No Longer Affected action on a cluster |
| OfficialUpdate | Institution-authored live update / scheduled disruption / advisory |
| CIVIS | Internal trust engine — computes WRAB, SDS, MACF |
| CSI-NLP | Free text → structured civic signal via Anthropic API |
| TDA | Temporal Distribution Aggregator — recurrence and pattern classification |
| Device | Installation anchor for CIVIS diversity checks |
| WardFollow | User followed ward — max 5 per account |
| RefreshToken | Server-side hashed refresh token tied to account + device |
| WRAB | Weighted Rolling Active Baseline — expected activity pressure |
| SDS | Signal Density Score — current activity vs baseline |
| MACF | Minimum Absolute Confirmation Floor — activation participation threshold |

---

## Locked implementation decisions

| Decision | Value |
|---|---|
| NLP provider | Anthropic Claude Sonnet, JSON output only |
| Auth (citizens) | Africa's Talking SMS OTP |
| Auth (web Phase 2) | Email magic link + TOTP |
| Spatial indexing | H3.net resolution 9 |
| Push notifications | Expo Push API |
| Geocoding | Nominatim / OSM (cached) |
| Schema management | EF Core code-first migrations |
| Join score formula | 0.40×category + 0.25×distance + 0.20×time + 0.15×condition |
| Join threshold | 0.65 |
| Restoration threshold | ≥60% ratio + ≥2 distinct affected device votes |
| Context edit window | 2 minutes post I'm Affected |
| Refresh token expiry | 30 days |
| JWT audience | hali-platform |

---

## Canonical Phase 1 API paths

```
POST /v1/auth/otp
POST /v1/auth/verify
POST /v1/auth/refresh
POST /v1/auth/logout
GET  /v1/home
POST /v1/signals/preview
POST /v1/signals/submit
GET  /v1/clusters/{id}
POST /v1/clusters/{id}/participation
POST /v1/clusters/{id}/context
POST /v1/clusters/{id}/restoration-response
GET  /v1/localities/followed
PUT  /v1/localities/followed
POST /v1/devices/push-token
PUT  /v1/users/me/notification-settings
GET  /v1/users/me
```

Phase 2 adds /v1/institution/* and /v1/institution-admin/*
Phase 3 adds /v1/ops/*

---

## Architecture reference documents

Read the relevant file before implementing each area. These are your implementation contracts.

| File | When to read |
|---|---|
| docs/arch/01_schema_reference.md | Before any DB migration — canonical DDL with all patches |
| docs/arch/02_api_contracts.md | Before any endpoint — all routes, shapes, error model |
| docs/arch/03_phase1_backend.md | Phase 1 backend — build gates, patterns, checklist |
| docs/arch/04_phase1_mobile.md | Phase 1 mobile — screens, navigation, offline queue |
| docs/arch/05_civis_engine.md | CIVIS, clustering, lifecycle — formulas in C# |
| docs/arch/06_worker_pipelines.md | All background workers and queue routing |
| docs/arch/07_auth_implementation.md | Auth for all surfaces, token rotation |
| docs/arch/08_phase2_institution.md | Institution dashboard surfaces (Phase 2) |
| docs/arch/09_nlp_integration.md | Anthropic API integration, validation, fallback |
| docs/arch/10_testing_strategy.md | Required unit, integration, and contract tests |

The Hali_Platform_Reconciliation_v1.md at repo root resolves all conflicts between earlier docs.
It supersedes any conflicting guidance in older files.

---

## Definition of done for every PR

- [ ] OpenAPI spec updated if any endpoint changed
- [ ] Unit tests cover all new domain rules and CIVIS logic
- [ ] Integration test covers the happy path for any new flow
- [ ] Structured log added for every operationally meaningful state change
- [ ] Audit entry written for every privileged action
- [ ] Outbox event emitted for every state-changing write
- [ ] No CIVIS internals (civis_score, wrab, sds, macf, raw_confirmation_count) in any public response
- [ ] No account_id or device_id in citizen-facing cluster responses
- [ ] No feature from the Phase 1 out-of-scope list

---

## What you must never do

- Add comments, likes, replies, chat, or messaging
- Expose raw signal_events on public feeds
- Expose civis_score, wrab, sds, macf, or reason codes in citizen or institution responses
- Allow an institution to suppress or remove a citizen cluster
- Allow final cluster resolution without citizen confirmation threshold
- Store refresh tokens as plaintext
- Store web session tokens in localStorage
- Import /packages/design-system into citizen-mobile
- Build microservices
- Replace H3 with geohash
- Skip outbox emission on any state-changing write

---

## Self-Healing Requirement (MANDATORY)

For any task involving CI pipelines, deployment workflows, database migrations,
Docker builds, or test failures:

1. Read `docs/arch/SELF_HEALING_SKILL.md` before starting
2. After every fix, watch the CI run yourself using `gh run watch`
3. Read failure logs yourself using `gh run view --log-failed`
4. Apply the next fix from the fix table in the skill file
5. Iterate until the job passes or you hit a genuine external blocker

Never stop and report a code-level failure to Irvine.
Never wait for Irvine to paste error logs.
You have gh CLI access. Use it every time without being asked.

Only defer to Irvine when the fix requires action outside the codebase:
missing GitHub secret, Neon infrastructure change, or external API credentials.

---


---

# PART 2 — BUILD STATE & ONGOING ENGINEERING

---

## Current build state

Phase 1 (Citizen Mobile MVP) backend is **complete**.

| Session | What was built | State |
|---|---|---|
| 00 | OpenAPI patched, EF Core migrations scaffolded, taxonomy seeded | ✅ Done |
| 01 | Repo structure, solution, all module projects, EF Core wiring | ✅ Done |
| 02 | Auth — SMS OTP, JWT, refresh tokens, device registration | ✅ Done |
| 03 | Signal intake, CSI-NLP extraction, geocoding, H3 spatial indexing | ✅ Done |
| 04 | Clustering, CIVIS engine (WRAB/SDS/MACF), activation gate, decay | ✅ Done |
| 05 | Participation, context window, vertical slice test (13 steps) | ✅ Done |
| 06 | Official updates, dual visibility, restoration lifecycle | ✅ Done |
| 07 | Push notifications, observability, integration polish | ✅ Done |

Post-build Agent C full-codebase validation: **PASS_WITH_NOTES** — all blocking issues resolved.
139 unit tests passing. Codebase on `main`.

**PR #28 merged:** Institution auth (B-5), OpenAPI v0.3.0 docs sync, SLOs (12 definitions), alert rules (14, P1–P4), runbook stubs (12), Dredd contract test hooks. 18/18 CI checks passed.

Repo location on WSL2: `/home/irvine/projects/halicity/hali`

---

## Agent C blocking issue status

| Issue | Description | Status |
|---|---|---|
| BLOCKING-1 | Integration tests — WebApplicationFactory + Testcontainers PostgreSQL | ⏳ Own session |
| BLOCKING-2 | Institution header bypass security fix | ✅ Done |
| BLOCKING-3 | Governance CIVIS constants | ✅ Done |
| BLOCKING-4 | Redis rate limiting on preview endpoint | ✅ Done |
| BLOCKING-5 | MinRestorationAffectedVotes corrected to 2 | ✅ Done |
| BLOCKING-6 | DeactivationThreshold documented | ✅ Done |
| BLOCKING-7 | Structured logging verified live | ✅ Done |

Integration tests (BLOCKING-1) are the only remaining backend item. Run as a parallel session alongside mobile app work.

---

## What is not yet built

| Item | Phase | Notes |
|---|---|---|
| Integration tests | Phase 1 | Separate session — can run in parallel with mobile |
| React Native mobile app (citizen-mobile) | Phase 1 | Agent D — highest priority |
| Institution Operations Dashboard | Phase 2 | Backend auth foundation (B-5) already done |
| Institution Admin Dashboard | Phase 2 | |
| Hali Ops Admin Dashboard | Phase 3 | |

The mobile app is the highest priority remaining Phase 1 item. Start with Agent D.

---

## How to work on the existing codebase

### Starting a new backend session

You do not run session prompts sequentially anymore — those are complete. For targeted backend work:

1. Read this file (auto-loaded)
2. Read the relevant `docs/arch/` file for the area you're working on
3. Read `docs/arch/00_session_patch_notes.md` for reconciliation context
4. State what you're changing and why before touching code
5. Run `dotnet test` before and after — must stay at 0 failures
6. Commit with a clear message referencing the issue or fix

### Starting a mobile app session (Agent D)

The React Native app is a greenfield build on top of the completed backend.
Before starting, read:
- `docs/arch/04_phase1_mobile.md` — full screen inventory and rules
- `docs/arch/02_api_contracts.md` — all Phase 1 endpoints the app calls
- `docs/arch/07_auth_implementation.md` — SMS OTP flow and token storage
- `agent_prompts/agent_d_mobile.md` — Agent D system prompt

The backend API is running at `http://localhost:8080` for local development.
Start with auth flow (phone entry → OTP → verify → home feed) and work screen by screen.

### Starting a Phase 2 session (institution dashboards)

Do not start Phase 2 until the mobile app is in pilot state.
When ready, read `docs/arch/08_phase2_institution.md` before anything else.
Phase 2 requires schema migrations for: `institution_memberships`, `institution_user_scopes`, `official_update_templates`, `institution_notification_recipients`.

---

## Multi-agent model (for ongoing sessions)

The agent orchestration framework is still intact and should be used for significant new workstreams.

| Agent | Use for |
|---|---|
| A | Backend feature work, bug fixes, new modules |
| B | Unit tests (runs independently from contracts, not from A's code) |
| C | Validation — run after any significant change set |
| D | React Native mobile app build |

Run agents via:
```bash
cd /home/irvine/projects/halicity/hali
python3 scripts/orchestrate.py --session mobile-01 --dry-run   # cost estimate first
python3 scripts/orchestrate.py --session mobile-01
```

Or trigger from GitHub Actions → Multi-Agent Orchestration.

Cost tracking: `agent_outputs/.spend.json` — reset at start of each billing month.

---

## PR workflow

Every significant change goes through a PR:
1. CI pipeline must be green (all jobs)
2. Agent C validation report in PR body
3. Test coverage ≥ 95% for domain + CIVIS modules
4. `irvinesunday` is the only reviewer (CODEOWNERS enforced)
5. Squash and merge

---

## Authority hierarchy (conflict resolution)

When any two documents conflict, this is the resolution order:

1. This file (CLAUDE.md)
2. Hali_Platform_Reconciliation_v1.md
3. docs/arch/00_session_patch_notes.md
4. docs/arch/* files by topic
5. 02_openapi.yaml for route names
6. Everything else (older .docx files, earlier .md files) — reference only

---

## Copilot Review Comments

Before addressing any Copilot review comment on any PR, read
`docs/arch/COPILOT_RESOLUTION_SKILL.md` in full. Every step in that
file is mandatory. Follow the skill file's exact review, thread-
reply, and resolution procedure, including its required checks and
all affected fixes. Do not skip the reply or resolve steps.

## Reference Skills

These instruction docs are mandatory reading at the listed trigger points:

- Read `mvp_locked_decisions.md` before changing any locked architectural, schema, or auth decision — this is the locked-decisions authority referenced by `docs/arch/COPILOT_RESOLUTION_SKILL.md`.
- Read `nlp_extraction_prompt.md` before modifying CSI-NLP extraction prompts, schemas, or the structured output contract.
- Read `mobile_screen_inventory.md` before adding, removing, or restructuring screens in `apps/citizen-mobile`.

## Architecture Reference Docs

The following are canonical reference documents — read them when working in their respective domains:

- `docs/arch/README.md` — index of the docs/arch/ canon
- `docs/arch/hali_citizen_mvp_canonical_spec.md` — Phase 1 citizen mobile UX/architecture freeze
- `docs/arch/hali_institution_dashboard_canonical_spec.md` — Phase 2 institution dashboard freeze
- `docs/arch/hali_ops_internal_dashboard_canonical_spec_v2.md` — Phase 3 Hali ops dashboard freeze
- `docs/runbooks/*.md` — incident response runbooks (read when touching the corresponding subsystem or responding to alerts)
- `docs/staging-env-guide.md` — staging environment / GitHub Environment secrets reference
- `04_queue_topic_definitions.md` — domain event and queue topic catalog
- `05_redis_job_config.md` — Redis usage and worker queue config
- `schema_patch_notes.md` — historical schema patch notes (supplements `docs/arch/01_schema_reference.md`)
- `HANDOVER.md` — operator guide for running Claude Code build sessions
- `SECURITY.md` — vulnerability reporting policy

## Code Quality — Mandatory Pre-Session and Pre-Commit Rules

### Before writing any code in a session
Read `docs/arch/CODING_STANDARDS.md` in full.
Read `docs/arch/LESSONS_LEARNED.md` — especially the most recent 3 entries.
These files contain concrete rules derived from real Copilot review failures.
Ignoring them will produce the same errors again.

### Before every git commit
Run the Pre-Commit Checklist in `docs/arch/CODING_STANDARDS.md`.
Every item must be checked. Do not commit if any item fails.

Minimum commands before committing C# changes:
  dotnet format --verify-no-changes
  dotnet build
  dotnet test (if tests exist for the changed module)

Minimum commands before committing TypeScript changes:
  npx tsc --noEmit

### After addressing a Copilot review comment
In the same commit that fixes the issue:
1. Fix the code
2. Append a new entry to `docs/arch/LESSONS_LEARNED.md` using the template
3. Add or update the corresponding rule in `docs/arch/CODING_STANDARDS.md`

This is mandatory — not optional. The lesson must be recorded in the same
commit as the fix so the fix and the lesson are permanently linked in history.

# Hali MVP

**Hali** is a neutral civic signal platform — a "civic weather system" that aggregates citizen observations and official institutional updates into structured, real-time locality insights.

> *What's happening around me right now, and is anything being done about it?*

---

## What Hali does

Citizens report conditions in natural language ("Big potholes near National Oil, Nairobi West"). Hali extracts structured civic signals from that text, clusters related reports spatially and temporally, and activates public clusters only when evidence meets the CIVIS confidence threshold. Institutions see the same clusters and respond with official updates. Citizens confirm restoration. The loop closes.

---

## Tech stack

| Layer | Technology |
|-------|-----------|
| Backend API | ASP.NET Core 9 — modular monolith |
| Background workers | .NET Hosted Services |
| Database | PostgreSQL 16 + PostGIS 3.4 |
| Cache / queues | Redis 7 |
| Mobile app | React Native + Expo (TypeScript) |
| NLP extraction | Anthropic Claude API (Sonnet) |
| Spatial indexing | H3 resolution 9 |
| SMS / OTP | Africa's Talking |
| Push notifications | Expo Push API |
| Geocoding | Nominatim / OpenStreetMap |
| Container registry | GitHub Container Registry (ghcr.io) |

---

## Quick start (local development)

```bash
# 1. Clone and enter the repo
git clone https://github.com/irvinesunday/hali.git
cd hali

# 2. Copy env file and fill in your API keys
cp .env.example .env
# Edit .env — minimum required: ANTHROPIC_API_KEY, AFRICASTALKING_API_KEY

# 3. Start infrastructure
docker-compose up -d

# 4. Apply migrations
dotnet ef database update \
  --project src/Hali.Infrastructure \
  --startup-project src/Hali.Api

# 5. Seed taxonomy
psql $DATABASE_URL -f seed_taxonomy_expanded.sql

# 6. Run the API
dotnet run --project src/Hali.Api --urls http://localhost:8080

# 7. Health check
curl http://localhost:8080/health
```

---

## Repository structure

```
.github/
  workflows/          ← CI, CD, multi-agent orchestration, spec verifier
  CODEOWNERS          ← All PRs require owner review
  PULL_REQUEST_TEMPLATE.md
  dependabot.yml
agent_outputs/        ← Auto-generated agent outputs (not committed, see .gitignore)
  .spend.json         ← Cumulative API spend tracker
agent_prompts/        ← Agent A/B/C/D prompt files + self-learning lessons
docs/
  runbooks/           ← Migration rollback and operational procedures
  staging-env-guide.md
scripts/
  orchestrate.py      ← Multi-agent orchestration (A + B + C + D in parallel)
  create_github_issues.py ← Creates all epics, stories, tasks in GitHub
  protect_branches.py ← One-time branch protection setup
  spec_verifier.py    ← Post-CI lesson extraction
  requirements.txt
session_prompts/      ← 8 Claude Code session files (00–07)
  _universal_rules.md ← Checkpoint commits, amnesia recovery, versioning
src/
  Hali.Api/           ← REST API host + Dockerfile
  Hali.Application/   ← Use cases, commands, queries
  Hali.Domain/        ← Entities, enums, domain services
  Hali.Infrastructure/← DB, Redis, external adapters
  Hali.Workers/       ← Background jobs + Dockerfile
  Hali.Contracts/     ← Shared request/response types
tests/
  Hali.UnitTests/     ← Pure unit tests (no DB, no network)
  Hali.IntegrationTests/ ← Integration + vertical slice tests
  dredd-hooks.js      ← API contract test fixtures
claude.md             ← Master implementation authority for Claude Code
mvp_locked_decisions.md ← All locked implementation decisions
02_openapi.yaml       ← API contract (authority for all endpoint paths)
01_postgres_schema.sql ← Schema DDL reference (EF Core migrations are source of truth)
```

---

## CI pipeline

Every push runs 9 jobs in dependency order:

```
Secret Scanning → OpenAPI Lint
                            ↓
                          Build
                         ↙     ↘
              Unit Tests    CIVIS Tests
                         ↘     ↙
                   Integration Tests
                           ↓
                    Contract Test (Dredd)
                           ↓
                  Coverage Gate (≥95%)
                           ↓
                   Vertical Slice (E2E)
```

PRs to `main` or `develop` cannot merge until all 9 jobs pass.

---

## Build process (Claude Code sessions)

The MVP is built across 8 Claude Code sessions. Each session has a dedicated prompt file in `session_prompts/`. Run them in order, one session at a time.

```
Session 00 — Sync patch (OpenAPI, migrations, seed)
Session 01 — Repo scaffold + EF Core migrations
Session 02 — Auth (OTP, JWT, refresh tokens, devices)
Session 03 — Signal ingestion + NLP + geocoding
Session 04 — Clustering + CIVIS (WRAB, SDS, MACF, decay)
Session 05 — Participation + vertical slice test
Session 06 — Official updates + restoration
Session 07 — Notifications + observability + final polish
```

See `HANDOVER.md` for the complete build guide.

---

## Multi-agent system

The orchestrator (`scripts/orchestrate.py`) runs three agents for each session:

- **Agent A** — writes backend implementation code (reads lessons from prior sessions first)
- **Agent B** — writes unit tests in parallel, from the contract only (never sees Agent A's code)
- **Agent C** — validates both outputs against the spec, produces a YES/NO merge verdict and a lessons block
- **Agent D** — writes React Native mobile screens in parallel

```bash
# Dry run: see token count and cost estimate before calling the API
python3 scripts/orchestrate.py --session 04 --dry-run

# Run a session
export ANTHROPIC_API_KEY=...
export GITHUB_TOKEN=...
export GITHUB_REPO=irvinesunday/hali
python3 scripts/orchestrate.py --session 04
```

---

## Branch protection

`main` and `develop` are protected. All changes go through PRs. No direct push.

To set up branch protection after creating the repo (run once):

```bash
export GITHUB_TOKEN=your_pat_with_repo_scope
export GITHUB_REPO=irvinesunday/hali
export GITHUB_OWNER_LOGIN=irvinesunday
python3 scripts/protect_branches.py
```

---

## Key documents

| Document | Purpose |
|----------|---------|
| `claude.md` | Master authority — Claude Code reads this first every session |
| `mvp_locked_decisions.md` | All locked implementation decisions |
| `HANDOVER.md` | Complete build handover guide |
| `02_openapi.yaml` | API contract — authority for all endpoint paths |
| `nlp_extraction_prompt.md` | NLP extraction contract and field mapping |
| `docs/runbooks/migration-rollback.md` | What to do when a production migration fails |
| `docs/staging-env-guide.md` | Environment separation rules |
| `agent_prompts/agent_a_lessons.md` | Agent A's accumulated self-learning checklist |

---

## One-time setup checklist

- [ ] Replace `@irvinesunday` in `CODEOWNERS`, `dependabot.yml`, and `protect_branches.py`
- [ ] Run `python3 scripts/protect_branches.py` after repo creation
- [ ] Run `python3 scripts/create_github_issues.py` to create GitHub issues
- [ ] Create GitHub Environment `production` with required reviewer = you
- [ ] Create GitHub Environment `staging` (no required reviewer)
- [ ] Add secrets: `ANTHROPIC_API_KEY`, `EXPO_ACCESS_TOKEN`, `AFRICASTALKING_API_KEY`, `STAGING_DATABASE_URL`, `STAGING_DB_PASSWORD`, `PRODUCTION_DATABASE_URL`
- [ ] Copy `.env.example` to `.env` and fill in local dev values
- [ ] Run Session 00 as the first Claude Code session

---

## Licence

Private — all rights reserved.

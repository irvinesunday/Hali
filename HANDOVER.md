# Hali MVP — Build Handover Guide

Complete operator guide for running the Claude Code build sessions and multi-agent system.

---

## Before your first session — one-time setup

### 1. Repository
```bash
git clone https://github.com/irvinesunday/hali.git
cd hali
cp .env.example .env       # fill in API keys
docker-compose up -d       # start PostgreSQL, Redis, OTel collector
```

### 2. Replace the placeholder username
Search for `irvinesunday` and replace with your actual GitHub username in:
- `.github/CODEOWNERS`
- `.github/dependabot.yml`
- `.github/ISSUE_TEMPLATE/bug_report.md`
- `.github/ISSUE_TEMPLATE/feature_request.md`
- `scripts/protect_branches.py`

### 3. Branch protection (run once after repo creation)
```bash
export GITHUB_TOKEN=your_pat_with_repo_scope
export GITHUB_REPO=irvinesunday/hali
export GITHUB_OWNER_LOGIN=irvinesunday
python3 scripts/protect_branches.py
```
Enforces on `main`: 9 required CI jobs, 1 required review (you), no direct push, no force push, admin bypass disabled.

### 4. GitHub issues (run once)
```bash
export GITHUB_TOKEN=your_pat
export GITHUB_REPO=irvinesunday/hali
python3 scripts/create_github_issues.py
```
Creates 7 epics, 23 user stories, 117 task issues with milestones and labels.

### 5. GitHub Environments
In Settings → Environments:
- `production` → Required reviewer: your GitHub username
- `staging` → no required reviewers (auto-deploys on develop merge)

Add environment secrets:
- `STAGING_DATABASE_URL`, `STAGING_DB_PASSWORD`
- `PRODUCTION_DATABASE_URL`
- `ANTHROPIC_API_KEY`, `EXPO_ACCESS_TOKEN`, `AFRICASTALKING_API_KEY`

---

## Session order (strict)

| Session file | What it builds | Gate |
|---|---|---|
| `session_00_sync.md` | OpenAPI patch, migrations scaffold, taxonomy seed | `SESSION_00_COMPLETE` |
| `session_01_foundation.md` | Repo structure, EF Core migrations | `dotnet build` passes |
| `session_02_auth.md` | OTP, JWT, refresh tokens, devices | Auth round-trip works |
| `session_03_intake.md` | Signal ingestion, NLP, geocoding | Preview + submit work |
| `session_04_clustering_civis.md` | H3 clustering, WRAB/SDS/MACF, decay | Activation gate passes |
| `session_05_participation_slice.md` | Participation + 13-step vertical slice | All 13 steps pass |
| `session_06_updates_restoration.md` | Official updates, restoration | Full loop resolves |
| `session_07_notifications_polish.md` | Notifications, health endpoint, observability | `dotnet test` = 0 failures |

Commit between every session: `git commit -m "build: session N complete"`

---

## How to start each session

Paste the contents of the session file as your first message to Claude Code.
`claude.md` is auto-read — do not paste it separately.

For sessions after 01, prepend one line:
> "Sessions 00–N are complete (commit: `<hash>`). Begin Session N+1."

**If a session runs out of context mid-way:**
1. `git commit -m "build: session N partial — completed: [X, Y]"`
2. Start a new session with the same session file
3. Open with: "Continuing Session N. Already committed: [X, Y]. Remaining: [Z]."

---

## Multi-agent orchestration

```bash
# Estimate cost first (no API calls)
python3 scripts/orchestrate.py --session 04 --dry-run

# Run a session
export ANTHROPIC_API_KEY=...
export GITHUB_TOKEN=...
export GITHUB_REPO=irvinesunday/hali
export MONTHLY_API_BUDGET_USD=20   # default is $50
python3 scripts/orchestrate.py --session 04
```

Or via GitHub Actions: Actions → Multi-Agent Orchestration → Run workflow → pick session.

The orchestrator runs Agents A + B + D in parallel, validates structurally, runs Agent C, extracts lessons, commits lessons (even on failure), then creates a PR.

**Cumulative spend is tracked in `agent_outputs/.spend.json`. Reset to zero each billing month.**

---

## Your PR review workflow

1. Check all 9 CI jobs are green (including Coverage Gate ≥ 95%)
2. Read Agent C's validation report in the PR body
3. Check the coverage comment posted to the PR
4. Approve → Squash and merge
5. Feature branch is auto-deleted

You are the only required approver (enforced by `CODEOWNERS` + branch protection).

---

## Key files

| File | Purpose |
|---|---|
| `claude.md` | Master authority — Claude Code reads this first |
| `mvp_locked_decisions.md` | All locked implementation decisions |
| `02_openapi.yaml` | API contract — authority for all endpoint paths |
| `nlp_extraction_prompt.md` | NLP extraction contract |
| `agent_prompts/agent_a_lessons.md` | Agent A's self-learning checklist (grows each session) |
| `agent_outputs/.spend.json` | Running API cost tracker |
| `docs/runbooks/migration-rollback.md` | What to do when a production migration fails |
| `docs/staging-env-guide.md` | Environment separation rules |

---

## Vertical slice (Session 05 gate — all 13 must pass before Session 06)

```
1.  Auth: POST /v1/auth/otp → /v1/auth/verify → get tokens
2.  Preview: free text → category=roads, subcategory=potholes
3.  Submit: signal_event row with lat/lng and NLP confidence fields
4.  Cluster created: state=unconfirmed
5.  Two more signals from different device fingerprints submitted
6.  CIVIS activation: cluster state=active
7.  Participation: I'm Affected recorded (affected_count=1)
8.  Context: Add Further Context accepted within window
9.  Context rejected after CIVIS_CONTEXT_EDIT_WINDOW_MINUTES (422)
10. GET /v1/clusters/{id}: state=active, affected_count=1
11. Restoration response: POST /v1/clusters/{id}/restoration-response
12. GET /v1/clusters/{id}: state=possible_restoration
13. GET /v1/home: cluster appears in activeNow section
```

---

## Adjusting the 95% coverage threshold

One line in `.github/workflows/ci.yml`:
```yaml
env:
  COVERAGE_THRESHOLD: 95   # change this number
```

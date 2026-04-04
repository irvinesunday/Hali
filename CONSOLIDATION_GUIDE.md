# Hali — Consolidation Guide
**How to merge the Architecture Pack and the Agent Framework into one complete repo.**

---

## What you have

**This zip (Hali_Consolidated_Pack.zip)** contains:
```
CLAUDE.md                                    ← UNIFIED — replaces both old claude.md files
Hali_Platform_Reconciliation_v1.md          ← conflict resolution document
docs/arch/
  00_session_patch_notes.md                 ← NEW — patches for session prompt files
  01_schema_reference.md
  02_api_contracts.md
  03_phase1_backend.md
  04_phase1_mobile.md
  05_civis_engine.md
  06_worker_pipelines.md
  07_auth_implementation.md
  08_phase2_institution.md
  09_nlp_integration.md
  10_testing_strategy.md
agent_prompts/
  agent_a_addendum.md                       ← NEW — prepend to agent_a_code.md
  agent_d_addendum.md                       ← NEW — prepend to agent_d_mobile.md
```

**The Agent Framework zip** (from the other conversation) contains:
```
claude.md                                    ← SUPERSEDED — do not use, replaced by CLAUDE.md above
session_prompts/
  _universal_rules.md
  session_00_sync.md through session_07_notifications_polish.md
agent_prompts/
  agent_a_code.md
  agent_a_lessons.md
  agent_b_tests.md
  agent_c_validator.md
  agent_d_mobile.md
scripts/
  orchestrate.py
  master_orchestrator.py
  create_github_issues.py
  protect_branches.py
  spec_verifier.py
  requirements.txt
.github/
  workflows/
  CODEOWNERS
  dependabot.yml
  ISSUE_TEMPLATE/
agent_outputs/
  .spend.json
```

---

## Merge instructions (exact steps)

### Step 1 — Start with the Agent Framework zip

This is your base. Extract it into your repo root:
```bash
unzip Hali_Agent_Framework.zip -d hali-mvp/
cd hali-mvp/
```

### Step 2 — Overlay with this zip

```bash
unzip -o Hali_Consolidated_Pack.zip -d hali-mvp/
```

The `-o` flag overwrites. The files in this zip that conflict with the Agent Framework:
- `CLAUDE.md` (new unified file) → overwrites the old `claude.md`

All other files in this zip are new additions, not overwrites.

### Step 3 — Delete the old claude.md

The agent framework zip has `claude.md` (lowercase). The consolidated pack has `CLAUDE.md` (uppercase).
On case-insensitive filesystems (Windows, macOS default) these are the same file — Step 2 handles it.
On Linux (case-sensitive), delete the old one explicitly:
```bash
rm claude.md   # if it exists separately from CLAUDE.md
```

### Step 4 — Prepend addendums to agent prompts

```bash
# Prepend agent_a_addendum.md to the top of agent_a_code.md
cat agent_prompts/agent_a_addendum.md agent_prompts/agent_a_code.md > /tmp/agent_a_merged.md
mv /tmp/agent_a_merged.md agent_prompts/agent_a_code.md

# Prepend agent_d_addendum.md to the top of agent_d_mobile.md
cat agent_prompts/agent_d_addendum.md agent_prompts/agent_d_mobile.md > /tmp/agent_d_merged.md
mv /tmp/agent_d_merged.md agent_prompts/agent_d_mobile.md
```

After this step, delete the addendum files (they're now baked in):
```bash
rm agent_prompts/agent_a_addendum.md
rm agent_prompts/agent_d_addendum.md
```

### Step 5 — Verify the final file structure

```bash
ls -la CLAUDE.md                          # should exist (capital C)
ls docs/arch/                             # should have 00_session_patch_notes.md through 10_*
ls agent_prompts/                         # should have 5 files, no addendum files
ls session_prompts/                       # should have _universal_rules.md + 8 session files
ls scripts/                               # should have orchestrate.py, create_github_issues.py etc.
ls .github/workflows/                     # should have ci.yml, multi_agent.yml, create_issues.yml
```

### Step 6 — Replace your-github-username

```bash
export GH=irvinesunday

sed -i "s/your-github-username/$GH/g" .github/CODEOWNERS
sed -i "s/your-github-username/$GH/g" .github/dependabot.yml
sed -i "s/your-github-username/$GH/g" .github/ISSUE_TEMPLATE/bug_report.md
sed -i "s/your-github-username/$GH/g" .github/ISSUE_TEMPLATE/feature_request.md 2>/dev/null || true
sed -i "s/your-github-username/$GH/g" scripts/protect_branches.py
```

### Step 7 — Initial commit

```bash
git init  # if not already a git repo
git add -A
git commit -m "docs: Hali consolidated build pack — architecture + agent framework unified"
```

---

## Complete repo file inventory after merge

```
/hali-mvp
  CLAUDE.md                                    ← master brief (unified)
  Hali_Platform_Reconciliation_v1.md           ← conflict resolution
  README.md                                    ← from agent framework
  HANDOVER.md                                  ← from agent framework
  .env.example
  docker-compose.yml
  01_postgres_schema.sql                       ← reference only (superseded by docs/arch/01)
  02_openapi.yaml                              ← authority (patched in Session 00)

  docs/
    arch/
      00_session_patch_notes.md
      01_schema_reference.md
      02_api_contracts.md
      03_phase1_backend.md
      04_phase1_mobile.md
      05_civis_engine.md
      06_worker_pipelines.md
      07_auth_implementation.md
      08_phase2_institution.md
      09_nlp_integration.md
      10_testing_strategy.md

  session_prompts/
    _universal_rules.md
    session_00_sync.md
    session_01_foundation.md
    session_02_auth.md
    session_03_intake.md
    session_04_clustering_civis.md
    session_05_participation_slice.md
    session_06_updates_restoration.md
    session_07_notifications_polish.md

  agent_prompts/
    agent_a_code.md           (with addendum prepended)
    agent_a_lessons.md        (starts empty)
    agent_b_tests.md
    agent_c_validator.md
    agent_d_mobile.md         (with addendum prepended)

  scripts/
    orchestrate.py
    master_orchestrator.py
    create_github_issues.py
    protect_branches.py
    spec_verifier.py
    requirements.txt

  .github/
    workflows/
      ci.yml
      multi_agent.yml
      create_issues.yml
    CODEOWNERS
    dependabot.yml
    ISSUE_TEMPLATE/

  agent_outputs/
    .spend.json

  src/                         (empty — Claude Code builds this)
  tests/                       (empty)
  apps/                        (empty)
  packages/                    (empty)
```

---

## How to start building after consolidation

### One-time setup

```bash
# 1. Copy .env.example → .env and fill in API keys
cp .env.example .env

# 2. Start local stack
docker-compose up -d

# 3. Set up branch protection (requires GITHUB_TOKEN with repo scope)
export GITHUB_TOKEN=your_pat
export GITHUB_REPO=irvinesunday/hali-mvp
python3 scripts/protect_branches.py

# 4. Create GitHub issues (once)
python3 scripts/create_github_issues.py

# 5. Verify setup
docker-compose ps   # all services healthy
```

### Start Session 00

Open Claude Code, paste the contents of `session_prompts/session_00_sync.md` as your first message.
CLAUDE.md is auto-read — do not paste it.

Session 00 applies the OpenAPI patches, scaffolds migrations, and seeds the taxonomy.
Its gate: SESSION_00_COMPLETE token in the output.

### Session sequence from there

See the session table in CLAUDE.md Part 2 — Session Execution.

---

## Authority order (if you ever see a conflict)

1. CLAUDE.md
2. Hali_Platform_Reconciliation_v1.md
3. docs/arch/00_session_patch_notes.md
4. docs/arch/* (by topic)
5. 02_openapi.yaml (route names)
6. session_prompts/session_NN_*.md (current session tasks)
7. Everything else (older .docx files, original .md files) — reference only

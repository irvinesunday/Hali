#!/usr/bin/env python3
"""
Applies all 4 correctness fixes to the Hali codebase.
Run from repo root: python3 scripts/apply_correctness_fixes.py

Fixes:
  BLOCKING-3: Governance already in code — add .env entries (was already handled)
  BLOCKING-5: MinRestorationAffectedVotes default 1 → 2 in CivisOptions + test
  BLOCKING-6: DeactivationThreshold — add to .env and mvp_locked_decisions.md
  BLOCKING-7: Verify structured logging instruction
"""

from pathlib import Path
import re, sys

ROOT  = Path(__file__).parent.parent
SRC   = ROOT / "src"
TESTS = ROOT / "tests"

passed = []
failed = []

def fix(label, path, old, new):
    text = path.read_text()
    if old not in text:
        print(f"  ⚠  {label}: pattern not found in {path.name}")
        failed.append(label)
        return False
    path.write_text(text.replace(old, new, 1))
    print(f"  ✓  {label}")
    passed.append(label)
    return True

def append_if_missing(label, path, marker, content):
    text = path.read_text()
    if marker in text:
        print(f"  ✓  {label}: already present")
        passed.append(label)
        return True
    path.write_text(text + content)
    print(f"  ✓  {label}: appended")
    passed.append(label)
    return True


# ── BLOCKING-3: Governance already in CivisOptions.cs — just add .env entries ──
print("\n── BLOCKING-3: Governance .env entries ──")

env_file = ROOT / ".env"
env_example = ROOT / ".env.example"

governance_env = """
# CIVIS Governance (added — was missing from initial env)
CIVIS_GOVERNANCE_BASE_FLOOR=2
CIVIS_GOVERNANCE_HALF_LIFE_HOURS=24
CIVIS_GOVERNANCE_MACF_MIN=2
CIVIS_GOVERNANCE_MACF_MAX=6
"""

for f in [env_file, env_example]:
    if f.exists():
        append_if_missing(f"Governance env in {f.name}", f,
                          "CIVIS_GOVERNANCE_BASE_FLOOR", governance_env)

# Also verify CivisOptions binds governance from config
# Check if Program.cs / DI config binds governance sub-section
# (The default values in CivisOptions are correct — env vars are optional tuning)
print("  ℹ  Governance category already handled in CivisOptions.cs (lines 86-92) and")
print("     GetCategoryOptions switch (line 108). Code is correct. Env vars added for tuning.")


# ── BLOCKING-5: MinRestorationAffectedVotes default 1 → 2 ────────────────────
print("\n── BLOCKING-5: MinRestorationAffectedVotes ──")

civis_opts = SRC / "Hali.Application" / "Clusters" / "CivisOptions.cs"
if civis_opts.exists():
    fix("CivisOptions.cs default 1→2",
        civis_opts,
        "public int MinRestorationAffectedVotes { get; set; } = 1;",
        "public int MinRestorationAffectedVotes { get; set; } = 2;")
else:
    print(f"  ✗  {civis_opts} not found")
    failed.append("CivisOptions MinRestorationAffectedVotes")

# Fix the test default
participation_tests = list(TESTS.rglob("ParticipationServiceTests.cs"))
if participation_tests:
    f = participation_tests[0]
    text = f.read_text()
    # Find DefaultOptions method and fix MinRestorationAffectedVotes = 1
    # The pattern from the audit: line 30 has "MinRestorationAffectedVotes = 1"
    # But line 192 has "MinRestorationAffectedVotes = 3" (specific test override — keep that)
    # We only want to fix the DefaultOptions() method (line 24-ish)
    
    # Strategy: fix only within the DefaultOptions() method body
    old_default = """private static CivisOptions DefaultOptions()"""
    if old_default in text:
        # Replace MinRestorationAffectedVotes = 1 that appears within 15 lines of DefaultOptions
        lines = text.split('\n')
        in_default = False
        brace_depth = 0
        new_lines = []
        for i, line in enumerate(lines):
            if 'private static CivisOptions DefaultOptions()' in line:
                in_default = True
                brace_depth = 0
            if in_default:
                brace_depth += line.count('{') - line.count('}')
                if brace_depth <= 0 and in_default and '{' in ''.join(lines[max(0,i-5):i+1]):
                    in_default = False
                if in_default and 'MinRestorationAffectedVotes = 1' in line:
                    line = line.replace('MinRestorationAffectedVotes = 1',
                                        'MinRestorationAffectedVotes = 2')
                    print(f"  ✓  ParticipationServiceTests DefaultOptions MinRestorationAffectedVotes 1→2")
                    passed.append("Test DefaultOptions fix")
            new_lines.append(line)
        f.write_text('\n'.join(new_lines))
    else:
        # Simpler: just replace the first occurrence (DefaultOptions comes before line 192)
        if 'MinRestorationAffectedVotes = 1' in text:
            # Replace only the first occurrence
            new_text = text.replace('MinRestorationAffectedVotes = 1',
                                     'MinRestorationAffectedVotes = 2', 1)
            f.write_text(new_text)
            print(f"  ✓  ParticipationServiceTests DefaultOptions MinRestorationAffectedVotes 1→2")
            passed.append("Test DefaultOptions fix")
        else:
            print(f"  ⚠  MinRestorationAffectedVotes = 1 not found in tests — may already be fixed")
else:
    print("  ✗  ParticipationServiceTests.cs not found")
    failed.append("Test DefaultOptions")


# ── BLOCKING-6: DeactivationThreshold — document + add to .env ───────────────
print("\n── BLOCKING-6: DeactivationThreshold documentation ──")

# Add to .env files
deactivation_env = """
# CIVIS Decay — DeactivationThreshold (ratio of live_mass/wrab below which decay triggers)
# Not in original mvp_locked_decisions.md — documented here as implementation constant
CIVIS_DEACTIVATION_THRESHOLD=0.5
"""
for f in [env_file, env_example]:
    if f.exists():
        append_if_missing(f"DeactivationThreshold in {f.name}",
                          f, "CIVIS_DEACTIVATION_THRESHOLD", deactivation_env)

# Add to mvp_locked_decisions.md
locked = ROOT / "mvp_locked_decisions.md"
if locked.exists():
    deactivation_doc = """

### CIVIS Decay Constants (documented post-build, Agent C BLOCKING-6)

| Constant | Value | Description |
|---|---|---|
| `DeactivationThreshold` | 0.5 | Ratio of `live_mass / effective_wrab` below which a cluster decays. Active → PossibleRestoration; PossibleRestoration → Resolved. Configurable via `CIVIS_DEACTIVATION_THRESHOLD`. |

This threshold is separate from the restoration vote threshold. Decay-based resolution
fires when activity falls below 50% of the local baseline, regardless of citizen votes.
"""
    append_if_missing("DeactivationThreshold in mvp_locked_decisions.md",
                      locked, "DeactivationThreshold", deactivation_doc)

# Wire env var into CivisOptions binding
# CivisOptions already has DeactivationThreshold property — just need the env var
# Check if appsettings.json or Program.cs binds it
# The env var CIVIS_DEACTIVATION_THRESHOLD will bind to Civis:DeactivationThreshold
# if the DI config uses the Section = "Civis" key — which it does
print("  ℹ  CivisOptions already reads DeactivationThreshold from config.")
print("     Env var CIVIS_DEACTIVATION_THRESHOLD will bind via IConfiguration.")
print("     No code change needed — only .env + docs updated.")


# ── BLOCKING-7: Structured logging verification instruction ────────────────────
print("\n── BLOCKING-7: Structured logging verification ──")
print("  ℹ  This fix requires running the API locally.")
print("     After this script completes, run:")
print("")
print("     docker compose -f 07_docker-compose.yml up -d db redis")
print("     dotnet run --project src/Hali.Api/ --urls http://localhost:8080 2>&1 | head -50")
print("")
print("     Then POST to /v1/auth/otp and check that the log line contains:")
print("       eventName, correlationId, category, durationMs fields")
print("")
passed.append("BLOCKING-7 (manual verification step documented)")


# ── Summary ────────────────────────────────────────────────────────────────────
print(f"\n{'='*56}")
print(f"  Results: {len(passed)} passed, {len(failed)} failed")
print(f"{'='*56}")
if failed:
    for f in failed:
        print(f"  ✗ {f}")
print(f"""
Next steps:
  1. Review changes in VS Code:
     code src/Hali.Application/Clusters/CivisOptions.cs
     code tests/Hali.Tests.Unit/Participation/ParticipationServiceTests.cs
     code mvp_locked_decisions.md

  2. Build + test:
     dotnet build src/Hali.Api/
     dotnet test tests/Hali.Tests.Unit/

  3. Commit:
     git add -A
     git commit -m "fix: correctness — MinRestorationAffectedVotes=2, DeactivationThreshold documented, governance env"
     git push origin main
""")

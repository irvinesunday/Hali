#!/usr/bin/env python3
"""
Hali MVP — Multi-Agent Orchestration Script
Runs Agent A + B in parallel, then Agent C. Extracts and persists lessons.
Includes: retry with backoff, token counting, cost guard, dry-run mode,
          output validation, and amnesia recovery.

Usage:
    export ANTHROPIC_API_KEY=...
    export GITHUB_TOKEN=...
    export GITHUB_REPO=irvinesunday/hali
    export MONTHLY_API_BUDGET_USD=20   # optional cost cap
    python3 scripts/orchestrate.py --session 04
    python3 scripts/orchestrate.py --session 04 --dry-run   # estimate cost, no API calls

Requires: pip install anthropic PyGithub tenacity
"""

import os, sys, time, argparse, subprocess, threading, json
from datetime import datetime, timezone
from pathlib import Path
from anthropic import Anthropic, APIStatusError, APIConnectionError, APITimeoutError
from github import Github
from tenacity import (retry, stop_after_attempt, wait_exponential,
                      retry_if_exception_type, before_sleep_log)
import logging

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("orchestrator")

# ─── Config ────────────────────────────────────────────────────────────────────

ANTHROPIC_API_KEY      = os.environ.get("ANTHROPIC_API_KEY")
GITHUB_TOKEN           = os.environ.get("GITHUB_TOKEN")
GITHUB_REPO            = os.environ.get("GITHUB_REPO")
MODEL                  = "claude-sonnet-4-6"
MAX_OUTPUT_TOKENS      = 8192
CONTEXT_WARN_THRESHOLD = 150_000   # warn if input exceeds this many tokens
CONTEXT_HARD_LIMIT     = 190_000   # refuse to call if input exceeds this
MONTHLY_BUDGET_USD     = float(os.environ.get("MONTHLY_API_BUDGET_USD", "50"))

# Sonnet 4 pricing (update if pricing changes)
INPUT_COST_PER_MTOK  = 3.00   # $ per million input tokens
OUTPUT_COST_PER_MTOK = 15.00  # $ per million output tokens

LESSONS_FILE   = "agent_prompts/agent_a_lessons.md"
SPEND_FILE     = "agent_outputs/.spend.json"   # cumulative spend tracker
LESSONS_MARKER = "<!-- LESSONS_APPEND_MARKER — do not remove this line, orchestrator appends below it -->"

SESSIONS = {
    "00": ("session_00_sync.md",                "sync"),
    "01": ("session_01_foundation.md",           "foundation"),
    "02": ("session_02_auth.md",                "auth"),
    "03": ("session_03_intake.md",              "intake"),
    "04": ("session_04_clustering_civis.md",    "clustering-civis"),
    "05": ("session_05_participation_slice.md", "participation"),
    "06": ("session_06_updates_restoration.md", "updates-restoration"),
    "07": ("session_07_notifications_polish.md","notifications"),
    "mobile-01": ("session_mobile_01.md",       "citizen-mobile-app"),
}

ROOT = Path(__file__).parent.parent


# ─── Utilities ─────────────────────────────────────────────────────────────────

def read_file(p: str) -> str:
    path = ROOT / p
    return path.read_text() if path.exists() else ""

def git(cmd: str) -> str:
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, cwd=str(ROOT))
    if r.returncode != 0:
        raise RuntimeError(f"git failed: {cmd}\nSTDERR: {r.stderr.strip()}")
    return r.stdout.strip()


# ─── Token counting ────────────────────────────────────────────────────────────

def count_tokens(text: str) -> int:
    """Rough token count: Anthropic tokeniser averages ~3.5 chars/token for English code."""
    return len(text) // 3

def estimate_cost(input_tokens: int, output_tokens: int = MAX_OUTPUT_TOKENS) -> float:
    return (input_tokens / 1_000_000 * INPUT_COST_PER_MTOK +
            output_tokens / 1_000_000 * OUTPUT_COST_PER_MTOK)

def load_cumulative_spend() -> float:
    path = ROOT / SPEND_FILE
    if not path.exists():
        return 0.0
    try:
        return json.loads(path.read_text()).get("total_usd", 0.0)
    except Exception:
        return 0.0

def record_spend(amount_usd: float):
    path = ROOT / SPEND_FILE
    path.parent.mkdir(parents=True, exist_ok=True)
    current = load_cumulative_spend()
    data = {
        "total_usd": round(current + amount_usd, 4),
        "updated_at": datetime.now(timezone.utc).isoformat(),
        "budget_usd": MONTHLY_BUDGET_USD,
        "remaining_usd": round(MONTHLY_BUDGET_USD - current - amount_usd, 4),
    }
    path.write_text(json.dumps(data, indent=2))
    log.info(f"Spend recorded: ${amount_usd:.4f} | Total: ${data['total_usd']:.4f} | Remaining: ${data['remaining_usd']:.4f}")


# ─── Claude API call with retry ────────────────────────────────────────────────

RETRYABLE = (APIStatusError, APIConnectionError, APITimeoutError)

@retry(
    stop=stop_after_attempt(4),
    wait=wait_exponential(multiplier=2, min=4, max=60),
    retry=retry_if_exception_type(RETRYABLE),
    before_sleep=before_sleep_log(log, logging.WARNING),
    reraise=True,
)
def _call_api(client: Anthropic, system: str, user: str) -> tuple[str, int, int]:
    """Call the Anthropic API. Returns (text, input_tokens, output_tokens)."""
    response = client.messages.create(
        model=MODEL,
        max_tokens=MAX_OUTPUT_TOKENS,
        system=system,
        messages=[{"role": "user", "content": user}],
    )
    return response.content[0].text, response.usage.input_tokens, response.usage.output_tokens


def call_claude(system: str, user: str, label: str, dry_run: bool = False) -> str:
    """
    Call Claude with retry, token counting, and cost tracking.
    In dry_run mode prints estimates but makes no API calls.
    """
    input_tokens  = count_tokens(system + user)
    est_cost      = estimate_cost(input_tokens)
    cumulative    = load_cumulative_spend()

    # Context window guard
    if input_tokens > CONTEXT_HARD_LIMIT:
        raise RuntimeError(
            f"[{label}] Input too large: ~{input_tokens:,} tokens exceeds hard limit "
            f"of {CONTEXT_HARD_LIMIT:,}. Reduce context (schema excerpt, lessons) before calling."
        )
    if input_tokens > CONTEXT_WARN_THRESHOLD:
        log.warning(f"[{label}] Large context: ~{input_tokens:,} tokens (warn threshold: {CONTEXT_WARN_THRESHOLD:,})")

    # Budget guard
    if cumulative + est_cost > MONTHLY_BUDGET_USD:
        raise RuntimeError(
            f"[{label}] Budget guard: estimated cost ${est_cost:.4f} would exceed "
            f"monthly budget ${MONTHLY_BUDGET_USD:.2f} "
            f"(already spent ${cumulative:.4f}). "
            f"Set MONTHLY_API_BUDGET_USD higher or reset agent_outputs/.spend.json."
        )

    log.info(f"[{label}] ~{input_tokens:,} input tokens | est. cost ${est_cost:.4f} | "
             f"cumulative ${cumulative:.4f}/{MONTHLY_BUDGET_USD:.2f}")

    if dry_run:
        log.info(f"[{label}] DRY RUN — no API call made")
        return f"[DRY RUN — {label} would have cost ~${est_cost:.4f}]"

    client = Anthropic(api_key=ANTHROPIC_API_KEY)
    text, in_tok, out_tok = _call_api(client, system, user)
    actual_cost = estimate_cost(in_tok, out_tok)
    record_spend(actual_cost)
    log.info(f"[{label}] Done: {in_tok:,} in / {out_tok:,} out tokens | actual cost ${actual_cost:.4f}")
    return text


# ─── Output validation ─────────────────────────────────────────────────────────

def validate_agent_a(output: str) -> list[str]:
    """
    Structural checks on Agent A's output.
    Returns a list of problems (empty = OK).
    """
    problems = []
    if len(output) < 500:
        problems.append("Output suspiciously short — may be a refusal or incomplete response")
    if "public" not in output and "interface" not in output:
        problems.append("No 'public' or 'interface' keywords found — may not be real C# code")
    if "AGENT_A_CONTRACT:" not in output:
        problems.append("Missing AGENT_A_CONTRACT block — Agent B cannot write tests without it")
    if output.count("```") < 2:
        problems.append("No code blocks found — expected at least one C# code block")
    return problems

def validate_agent_b(output: str) -> list[str]:
    """Structural checks on Agent B's output."""
    problems = []
    if len(output) < 300:
        problems.append("Output suspiciously short")
    if "[Fact]" not in output and "[Theory]" not in output:
        problems.append("No xUnit [Fact] or [Theory] attributes found — may not be real tests")
    if "AGENT_B_TEST_SUMMARY:" not in output:
        problems.append("Missing AGENT_B_TEST_SUMMARY block")
    test_count = output.count("[Fact]") + output.count("[Theory]")
    if test_count < 3:
        problems.append(f"Only {test_count} test methods found — expected at least 3")
    return problems


# ─── Amnesia recovery ──────────────────────────────────────────────────────────

def build_amnesia_recovery(session_num: str) -> str:
    """
    Reads the last 5 git commits and recent agent outputs to build
    a continuity summary Claude Code can read at the start of a new session.
    """
    try:
        log = git("git log --oneline -10")
        recent_lessons = read_file(LESSONS_FILE)[-2000:]
        prior_contract = ""
        try:
            prior_session_int = int(session_num) - 1
            if prior_session_int >= 0:
                prior_key = f"{prior_session_int:02d}"
                prior_contract_path = f"agent_outputs/session_{prior_key}/agent_a.md"
                raw = read_file(prior_contract_path)
                if "AGENT_A_CONTRACT:" in raw:
                    start = raw.index("AGENT_A_CONTRACT:")
                    prior_contract = raw[start:start+1500]
        except ValueError:
            # Non-numeric session ID (e.g., mobile-01) — skip prior contract lookup
            pass

        return f"""## Continuity Check — Session {session_num}

Before writing any code, confirm you understand the current state of the repo.

### Last 10 git commits
```
{log}
```

### What the previous session built (from Agent A contract)
```
{prior_contract or "No prior session contract found."}
```

### Recent lessons (last 2000 chars)
```
{recent_lessons}
```

### Your continuity confirmation
Before starting, state in one paragraph:
1. What was built in the prior session
2. What you are about to build
3. Any conflicts or inconsistencies you see between them

If you see a conflict, STOP and describe it rather than proceeding.
"""
    except Exception as e:
        return f"## Continuity Check\n(Could not generate — {e})"


# ─── Extraction helpers ────────────────────────────────────────────────────────

def extract_block(text: str, marker: str) -> str:
    if marker not in text:
        return ""
    start = text.index(marker)
    end   = text.find("```", start + len(marker) + 10)
    return text[start:end].strip() if end != -1 else text[start:].strip()

def extract_contract(out):     return extract_block(out, "AGENT_A_CONTRACT:")     or "(No contract)"
def extract_test_summary(out): return extract_block(out, "AGENT_B_TEST_SUMMARY:") or "(No test summary)"
def extract_lessons(out):      return extract_block(out, "AGENT_C_LESSONS:")

def verdict_is_pass(out: str) -> bool:
    return "Approved to merge: YES" in out


# ─── Lessons persistence ────────────────────────────────────────────────────────

def append_lessons(session_num: str, phase_name: str, lessons_block: str):
    path = ROOT / LESSONS_FILE
    current = path.read_text()
    date = datetime.now(timezone.utc).strftime("%Y-%m-%d")

    if not lessons_block or "No new lessons" in lessons_block:
        entry = f"\n## Session {session_num} — {phase_name} ({date})\nClean session — no new lessons.\n"
    else:
        entry = f"\n## Session {session_num} — {phase_name} ({date})\n\n{lessons_block}\n"

    updated = (current.replace(LESSONS_MARKER, entry + "\n" + LESSONS_MARKER)
               if LESSONS_MARKER in current else current + "\n" + entry)
    path.write_text(updated)
    log.info(f"Lessons persisted to {LESSONS_FILE}")

def commit_lessons(session_num: str, phase_name: str, suffix: str = ""):
    try:
        git(f"git add {LESSONS_FILE}")
        git(f"git add {SPEND_FILE} 2>/dev/null || true")
        msg = f"lessons: session {session_num} {phase_name}" + (f" — {suffix}" if suffix else "")
        git(f'git commit -m "{msg}"')
    except RuntimeError:
        pass   # nothing to commit


# ─── GitHub PR ─────────────────────────────────────────────────────────────────

def create_pr(session_num, phase_name, branch, contract, test_summary, agent_c_output):
    g    = Github(GITHUB_TOKEN)
    repo = g.get_repo(GITHUB_REPO)
    milestone = next(
        (m for m in repo.get_milestones(state="open") if m.title.startswith(f"{session_num} —")),
        None
    )
    spend = load_cumulative_spend()
    body = f"""## Session {session_num} — {phase_name}

Auto-generated by the Hali multi-agent build system.

| Metric | Value |
|--------|-------|
| Coverage gate | ≥95% (enforced by CI) |
| Session API cost | see agent_outputs/.spend.json |
| Cumulative spend | ${spend:.4f} |

### Agent A — Contract
```
{contract[:2000]}
```

### Agent B — Test Summary
```
{test_summary[:2000]}
```

### Agent C — Validation Report
```
{agent_c_output[-3000:]}
```
"""
    pr = repo.create_pull(
        title=f"[Session {session_num}] {phase_name}",
        body=body, head=branch, base="develop"
    )
    if milestone:
        pr.as_issue().edit(milestone=milestone)
    log.info(f"PR created: {pr.html_url}")
    return pr


# ─── Main orchestration ─────────────────────────────────────────────────────────

def run_session(session_num: str, dry_run: bool = False):
    if session_num not in SESSIONS:
        log.error(f"Unknown session: {session_num}. Valid: {list(SESSIONS.keys())}")
        sys.exit(1)

    session_file, phase_name = SESSIONS[session_num]
    mode = " [DRY RUN]" if dry_run else ""
    print(f"\n{'='*60}\n  Session {session_num}: {phase_name}{mode}\n{'='*60}\n")

    # ── Load all context ────────────────────────────────────────────────────
    session_prompt = read_file(f"session_prompts/{session_file}")
    lessons        = read_file(LESSONS_FILE)
    agent_a_base   = read_file("agent_prompts/agent_a_code.md")
    agent_b_base   = read_file("agent_prompts/agent_b_tests.md")
    agent_c_base   = read_file("agent_prompts/agent_c_validator.md")
    agent_d_base   = read_file("agent_prompts/agent_d_mobile.md")

    # ── Build amnesia recovery block (item 23) ──────────────────────────────
    amnesia = build_amnesia_recovery(session_num)

    # ── Inject lessons + amnesia into Agent A system prompt ─────────────────
    agent_a_system = (
        f"# LESSONS FROM PRIOR SESSIONS — READ BEFORE WRITING ANY CODE\n\n"
        f"```\n{lessons}\n```\n\n"
        f"{amnesia}\n\n"
        + agent_a_base
    )

    shared_ctx = (
        f"## claude.md\n{read_file('claude.md')}\n\n"
        f"## mvp_locked_decisions.md\n{read_file('mvp_locked_decisions.md')}\n\n"
        f"## 01_postgres_schema.sql (excerpt)\n{read_file('01_postgres_schema.sql')[:3000]}\n"
    )

    # ── Dry-run: estimate costs for all agents and exit ──────────────────────
    if dry_run:
        agents = [
            ("Agent A", agent_a_system, shared_ctx + "\n\n## Session task\n" + session_prompt),
            ("Agent B", agent_b_base,   shared_ctx + "\n\n## Session task\n" + session_prompt),
            ("Agent C", agent_c_base,   shared_ctx + "\n\n## Session task\n" + session_prompt),
        ]
        if agent_d_base:
            agents.append(("Agent D (mobile)", agent_d_base, shared_ctx + "\n\n## Session task\n" + session_prompt))

        total_est = 0.0
        print("DRY RUN — token and cost estimates:\n")
        for label, sys_p, usr_p in agents:
            tokens = count_tokens(sys_p + usr_p)
            cost   = estimate_cost(tokens)
            total_est += cost
            print(f"  {label}: ~{tokens:,} input tokens | est. ${cost:.4f}")
        cumulative = load_cumulative_spend()
        print(f"\n  Session total estimate: ${total_est:.4f}")
        print(f"  Cumulative spend so far: ${cumulative:.4f}")
        print(f"  Monthly budget: ${MONTHLY_BUDGET_USD:.2f}")
        print(f"  Remaining after session: ${MONTHLY_BUDGET_USD - cumulative - total_est:.4f}")
        if cumulative + total_est > MONTHLY_BUDGET_USD:
            print(f"\n  ⚠ WARNING: Would exceed monthly budget!")
        print("\n  Run without --dry-run to execute.")
        return

    # ── Create branches ──────────────────────────────────────────────────────
    branch_impl  = f"feature/session-{session_num}-{phase_name}-impl"
    branch_tests = f"feature/session-{session_num}-{phase_name}-tests"
    current = git("git rev-parse HEAD")
    for b in [branch_impl, branch_tests]:
        try: git(f"git checkout -b {b} {current}"); git("git checkout -")
        except RuntimeError: pass

    # ── Phase 1: Agent A + B in parallel ────────────────────────────────────
    print("Phase 1: Agent A (backend code) + Agent B (tests) running in parallel...\n")
    results, errors = {}, []

    def run_a():
        try:
            out = call_claude(agent_a_system,
                              f"{shared_ctx}\n\n## Session task\n{session_prompt}",
                              "Agent A", dry_run)
            problems = validate_agent_a(out)
            if problems:
                log.warning(f"[Agent A] Output validation issues:\n" + "\n".join(f"  - {p}" for p in problems))
                results["a_problems"] = problems
            results["a"] = out
        except Exception as e:
            errors.append(f"Agent A: {e}")

    def run_b():
        try:
            out = call_claude(agent_b_base,
                              f"{shared_ctx}\n\n## Session task\n{session_prompt}",
                              "Agent B", dry_run)
            problems = validate_agent_b(out)
            if problems:
                log.warning(f"[Agent B] Output validation issues:\n" + "\n".join(f"  - {p}" for p in problems))
                results["b_problems"] = problems
            results["b"] = out
        except Exception as e:
            errors.append(f"Agent B: {e}")

    # Run Agent D (mobile) in parallel too, if the prompt exists
    def run_d():
        if not agent_d_base:
            return
        try:
            out = call_claude(agent_d_base,
                              f"{shared_ctx}\n\n## Session task\n{session_prompt}",
                              "Agent D (mobile)", dry_run)
            results["d"] = out
        except Exception as e:
            log.warning(f"Agent D skipped: {e}")

    ta = threading.Thread(target=run_a)
    tb = threading.Thread(target=run_b)
    td = threading.Thread(target=run_d)
    ta.start(); tb.start(); td.start()
    ta.join();  tb.join();  td.join()

    if errors:
        log.error("Agent errors:\n" + "\n".join(errors))
        sys.exit(1)

    out_a = results.get("a", "")
    out_b = results.get("b", "")
    out_d = results.get("d", "")

    # Fail hard if Agent A or B had structural problems
    a_problems = results.get("a_problems", [])
    b_problems = results.get("b_problems", [])
    if a_problems or b_problems:
        log.error("Structural validation failed — not proceeding to Agent C.")
        log.error("Agent A problems: " + str(a_problems))
        log.error("Agent B problems: " + str(b_problems))
        log.error("Fix the session prompt or context and re-run.")
        sys.exit(1)

    out_dir = ROOT / "agent_outputs" / f"session_{session_num}"
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "agent_a.md").write_text(out_a)
    (out_dir / "agent_b.md").write_text(out_b)
    if out_d:
        (out_dir / "agent_d_mobile.md").write_text(out_d)

    contract     = extract_contract(out_a)
    test_summary = extract_test_summary(out_b)

    # ── Phase 2: Agent C validates ───────────────────────────────────────────
    print("\nPhase 2: Agent C validating...\n")
    c_user = (
        f"{shared_ctx}\n## Session task\n{session_prompt}\n"
        f"## Contract\n{contract}\n## Test summary\n{test_summary}\n"
        f"## Agent A output (first 5000 chars)\n{out_a[:5000]}\n"
        f"## Agent B output (first 5000 chars)\n{out_b[:5000]}"
    )
    if out_d:
        c_user += f"\n## Agent D mobile output (first 2000 chars)\n{out_d[:2000]}"

    out_c = call_claude(agent_c_base, c_user, "Agent C", dry_run)
    (out_dir / "agent_c.md").write_text(out_c)

    for line in out_c.split("\n"):
        if any(k in line for k in ("verdict", "Approved", "BLOCKING", "PASS", "FAIL")):
            print(f"  {line}")

    # ── Phase 3: Extract and persist lessons ─────────────────────────────────
    print("\nPhase 3: Persisting lessons...\n")
    lessons_block = extract_lessons(out_c)
    append_lessons(session_num, phase_name, lessons_block)

    if not verdict_is_pass(out_c):
        log.warning("Agent C: NOT approved. Fix BLOCKING items and re-run.")
        commit_lessons(session_num, phase_name, "failed run")
        sys.exit(1)

    print("\nPhase 4: Approved — committing and pushing...\n")
    commit_lessons(session_num, phase_name)

    git(f"git checkout {branch_impl}")
    git(f"git add agent_outputs/session_{session_num}/")
    git(f'git commit -m "session {session_num}: {phase_name} impl"')
    git(f"git push origin {branch_impl}")

    git(f"git checkout {branch_tests}")
    git(f"git add agent_outputs/session_{session_num}/agent_b.md")
    git(f'git commit -m "session {session_num}: {phase_name} tests"')
    git(f"git push origin {branch_tests}")

    git("git checkout -")

    if GITHUB_TOKEN and GITHUB_REPO:
        create_pr(session_num, phase_name, branch_impl,
                  contract, test_summary, out_c)

    print(f"\nSession {session_num} complete.")
    print(f"  Lessons:        {LESSONS_FILE}")
    print(f"  Spend tracker:  {SPEND_FILE}")
    print(f"  Agent outputs:  agent_outputs/session_{session_num}/")


if __name__ == "__main__":
    p = argparse.ArgumentParser(description="Hali multi-agent orchestrator")
    p.add_argument("--session",  required=True, help="Session ID (00-07, mobile-01)")
    p.add_argument("--dry-run",  action="store_true",
                   help="Estimate token count and cost without calling the API")
    args = p.parse_args()
    run_session(args.session, dry_run=args.dry_run)

#!/usr/bin/env python3
"""
Hali — Master Orchestrator
Runs the complete build pipeline end-to-end.
Invokes Claude Code (or multi-agent) per session, monitors CI,
tracks token spend in real time, and pauses only when human input is needed.

Usage:
    python3 scripts/master_orchestrator.py              # full run
    python3 scripts/master_orchestrator.py --dry-run    # estimate costs, no API calls
    python3 scripts/master_orchestrator.py --from-session 03
    python3 scripts/master_orchestrator.py --status
    python3 scripts/master_orchestrator.py --costs      # cost report only
    python3 scripts/master_orchestrator.py --mode multi-agent
    python3 scripts/master_orchestrator.py --reset      # clear state and start over

Required env vars:
    ANTHROPIC_API_KEY      — Anthropic API key
    GITHUB_TOKEN           — GitHub PAT (repo scope)
    GITHUB_REPO            — irvinesunday/hali
    GITHUB_OWNER_LOGIN     — irvinesunday
    MONTHLY_API_BUDGET_USD — optional, default $50
"""

import os, sys, json, time, subprocess, argparse, textwrap, re
from pathlib import Path
from datetime import datetime, timezone
from typing import Optional

try:
    import requests
    from tenacity import retry, stop_after_attempt, wait_exponential, retry_if_exception_type
except ImportError:
    print("Run: pip install -r scripts/requirements.txt")
    sys.exit(1)

# ─── Constants ─────────────────────────────────────────────────────────────────

ROOT             = Path(__file__).parent.parent
BUILD_STATE_FILE = ROOT / "build_state.json"
SPEND_FILE       = ROOT / "agent_outputs" / ".spend.json"
LOG_FILE         = ROOT / "agent_outputs" / "master_orchestrator.log"

ANTHROPIC_API_KEY = os.environ.get("ANTHROPIC_API_KEY", "")
GITHUB_TOKEN      = os.environ.get("GITHUB_TOKEN", "")
GITHUB_REPO       = os.environ.get("GITHUB_REPO", "irvinesunday/hali")
GITHUB_OWNER      = os.environ.get("GITHUB_OWNER_LOGIN", "irvinesunday")
MONTHLY_BUDGET    = float(os.environ.get("MONTHLY_API_BUDGET_USD", "50"))

GH_API     = "https://api.github.com"
GH_HEADERS = {
    "Authorization": f"Bearer {GITHUB_TOKEN}",
    "Accept": "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
}

# Sonnet 4.6 pricing (per million tokens)
INPUT_COST_PER_MTOK  = 3.00
OUTPUT_COST_PER_MTOK = 15.00
MAX_OUTPUT_TOKENS    = 8192

# Per-session token estimates (input tokens, based on context size)
# These are conservative — actual spend is tracked and updated after each session
SESSION_TOKEN_ESTIMATES = {
    "00": (25_000,  4_000),   # sync — small context
    "01": (40_000,  6_000),   # foundation — medium
    "02": (50_000,  7_000),   # auth
    "03": (65_000,  8_000),   # intake + NLP + geocoding
    "04": (80_000,  8_192),   # clustering + CIVIS — largest
    "05": (70_000,  8_192),   # participation + vertical slice
    "06": (60_000,  7_000),   # updates + restoration
    "07": (55_000,  7_000),   # notifications + polish
}

# Budget thresholds
BUDGET_WARN_PCT    = 0.30   # warn when < 30% remaining
BUDGET_CONFIRM_PCT = 0.15   # ask to confirm when < 15% remaining
BUDGET_BLOCK_PCT   = 0.05   # refuse to continue when < 5% remaining

# CI settings
CI_POLL_SECS   = 30
CI_MAX_WAIT    = 25 * 60   # 25 minutes
CI_RETRY_MAX   = 2

# Claude Code timeout per session
CC_TIMEOUT     = 5400   # 90 minutes

SESSIONS = {
    "00": ("session_00_sync.md",                 "sync"),
    "01": ("session_01_foundation.md",            "foundation"),
    "02": ("session_02_auth.md",                 "auth"),
    "03": ("session_03_intake.md",               "intake"),
    "04": ("session_04_clustering_civis.md",     "clustering-civis"),
    "05": ("session_05_participation_slice.md",  "participation"),
    "06": ("session_06_updates_restoration.md",  "updates-restoration"),
    "07": ("session_07_notifications_polish.md", "notifications"),
}


# ─── Spend tracking ────────────────────────────────────────────────────────────

def load_spend() -> dict:
    """Load the spend tracker. Returns dict with total_usd, budget_usd, sessions."""
    if SPEND_FILE.exists():
        try:
            return json.loads(SPEND_FILE.read_text())
        except Exception:
            pass
    return {
        "total_usd": 0.0,
        "budget_usd": MONTHLY_BUDGET,
        "remaining_usd": MONTHLY_BUDGET,
        "sessions": {},
        "updated_at": datetime.now(timezone.utc).isoformat(),
    }

def save_spend(data: dict):
    SPEND_FILE.parent.mkdir(parents=True, exist_ok=True)
    data["updated_at"] = datetime.now(timezone.utc).isoformat()
    data["remaining_usd"] = round(data["budget_usd"] - data["total_usd"], 4)
    SPEND_FILE.write_text(json.dumps(data, indent=2))

def record_session_spend(session_num: str, in_tok: int, out_tok: int, mode: str):
    """Record actual token usage after a session completes."""
    cost = (in_tok / 1_000_000 * INPUT_COST_PER_MTOK +
            out_tok / 1_000_000 * OUTPUT_COST_PER_MTOK)
    data = load_spend()
    data["total_usd"] = round(data.get("total_usd", 0) + cost, 4)
    if "sessions" not in data:
        data["sessions"] = {}
    data["sessions"][session_num] = {
        "input_tokens":  in_tok,
        "output_tokens": out_tok,
        "cost_usd":      round(cost, 4),
        "mode":          mode,
        "completed_at":  datetime.now(timezone.utc).isoformat(),
    }
    save_spend(data)
    return cost

def estimate_session_cost(session_num: str) -> tuple[float, int, int]:
    """Return (estimated_cost, est_input_tok, est_output_tok) for a session."""
    in_tok, out_tok = SESSION_TOKEN_ESTIMATES.get(session_num, (60_000, 7_000))

    # In multi-agent mode: 3 agents run per session (A + B + C), D optional
    # Use 3.5× the single-session estimate as a conservative multi-agent estimate
    # (A is largest, B and C are smaller)
    in_tok_total  = int(in_tok * 3.5)
    out_tok_total = int(out_tok * 3.0)

    cost = (in_tok_total / 1_000_000 * INPUT_COST_PER_MTOK +
            out_tok_total / 1_000_000 * OUTPUT_COST_PER_MTOK)
    return round(cost, 4), in_tok_total, out_tok_total

def get_budget_status() -> dict:
    """Return a full budget status dict."""
    data = load_spend()
    spent     = data.get("total_usd", 0.0)
    budget    = data.get("budget_usd", MONTHLY_BUDGET)
    remaining = budget - spent
    pct_used  = (spent / budget) if budget > 0 else 0
    pct_left  = 1 - pct_used
    return {
        "spent":     spent,
        "budget":    budget,
        "remaining": remaining,
        "pct_used":  pct_used,
        "pct_left":  pct_left,
        "sessions":  data.get("sessions", {}),
    }

def budget_bar(pct_used: float, width: int = 30) -> str:
    """ASCII progress bar for budget usage."""
    filled = int(width * pct_used)
    bar    = "█" * filled + "░" * (width - filled)
    if pct_used >= 0.85:   colour = "🔴"
    elif pct_used >= 0.70: colour = "🟡"
    else:                  colour = "🟢"
    return f"{colour} [{bar}] {pct_used*100:.1f}% used"

def forecast_remaining_sessions(done: set) -> tuple[float, dict]:
    """Estimate total cost of all sessions not yet complete."""
    pending = {n: v for n, v in SESSIONS.items() if n not in done}
    total = 0.0
    breakdown = {}
    for n in pending:
        cost, _, _ = estimate_session_cost(n)
        breakdown[n] = cost
        total += cost
    return round(total, 4), breakdown


# ─── Cost dashboard ────────────────────────────────────────────────────────────

def print_cost_dashboard(sessions_done: set = None):
    """Print a full cost dashboard to stdout."""
    bs     = get_budget_status()
    done   = sessions_done or set()

    print(f"\n{'━'*56}")
    print(f"  💰 Hali Cost Dashboard")
    print(f"{'━'*56}")
    print(f"  Monthly budget:  ${bs['budget']:.2f}")
    print(f"  Spent so far:    ${bs['spent']:.4f}")
    print(f"  Remaining:       ${bs['remaining']:.4f}")
    print(f"\n  {budget_bar(bs['pct_used'])}")

    if bs["sessions"]:
        print(f"\n  Session actuals:")
        for n, s in sorted(bs["sessions"].items()):
            name = SESSIONS.get(n, ("?",))[1] if n in SESSIONS else "?"
            print(f"    Session {n} ({name}): "
                  f"${s['cost_usd']:.4f}  "
                  f"({s['input_tokens']:,}in / {s['output_tokens']:,}out tokens)")

    pending_total, breakdown = forecast_remaining_sessions(done)
    if breakdown:
        print(f"\n  Remaining session estimates:")
        for n, cost in sorted(breakdown.items()):
            name = SESSIONS[n][1]
            print(f"    Session {n} ({name}): ~${cost:.4f}")
        print(f"  Forecast total remaining: ~${pending_total:.4f}")

        after_all = bs["spent"] + pending_total
        print(f"  Projected final spend:    ~${after_all:.4f} "
              f"({'OVER BUDGET ⚠' if after_all > bs['budget'] else 'within budget ✓'})")

    print(f"{'━'*56}\n")


# ─── Pre-session budget gate ───────────────────────────────────────────────────

def budget_gate(session_num: str, state: dict, dry_run: bool, mode: str):
    """
    Check budget before running a session.
    Shows cost estimate, remaining budget, and either warns or pauses.
    """
    bs             = get_budget_status()
    est_cost, in_t, out_t = estimate_session_cost(session_num)
    name           = SESSIONS[session_num][1]
    after          = bs["spent"] + est_cost
    pct_after      = after / bs["budget"] if bs["budget"] > 0 else 1

    log(f"")
    log(f"  ┌─ Budget check: Session {session_num} ({name}) {'[DRY RUN]' if dry_run else ''}")
    log(f"  │  Mode:           {mode}")
    log(f"  │  Est. tokens:    ~{in_t:,} in / {out_t:,} out")
    log(f"  │  Est. cost:      ~${est_cost:.4f}")
    log(f"  │  Spent so far:   ${bs['spent']:.4f}")
    log(f"  │  After session:  ~${after:.4f} / ${bs['budget']:.2f} budget")
    log(f"  │  Remaining:      ${bs['remaining']:.4f} ({bs['pct_left']*100:.1f}%)")
    log(f"  └─ {budget_bar(pct_after, width=20)}")

    if dry_run:
        return  # never block on dry run

    # Hard block: projected spend would exceed budget
    if after > bs["budget"]:
        pause(state,
              reason=f"Session {session_num} would exceed monthly budget",
              detail=f"Estimated cost ${est_cost:.4f} + spent ${bs['spent']:.4f} "
                     f"= ${after:.4f} exceeds ${bs['budget']:.2f} budget.\n"
                     f"Raise budget: export MONTHLY_API_BUDGET_USD={int(after)+5}\n"
                     f"Or reset monthly: edit agent_outputs/.spend.json → set total_usd to 0")

    # Near-empty: confirm before continuing
    if bs["pct_left"] < BUDGET_CONFIRM_PCT:
        log(f"  ⚠  Only {bs['pct_left']*100:.1f}% budget remaining (${bs['remaining']:.4f})", "WARN")
        answer = input(f"  Continue session {session_num}? This will use ~${est_cost:.4f}. [yes/no]: ").strip().lower()
        if answer != "yes":
            pause(state,
                  reason="User cancelled due to low budget",
                  detail=f"Resume when budget is replenished: python3 scripts/master_orchestrator.py --from-session {session_num}")

    # Low budget: warn only
    elif bs["pct_left"] < BUDGET_WARN_PCT:
        log(f"  ⚠  Budget low ({bs['pct_left']*100:.1f}% remaining) — proceeding", "WARN")


# ─── Logging ───────────────────────────────────────────────────────────────────

LOG_FILE.parent.mkdir(parents=True, exist_ok=True)

def log(msg: str, level: str = "INFO"):
    ts   = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
    line = f"[{ts}] [{level}] {msg}"
    print(line)
    with open(LOG_FILE, "a") as f:
        f.write(line + "\n")

def log_section(title: str):
    bar = "─" * 56
    log(f"\n{bar}\n  {title}\n{bar}")


# ─── Build state ───────────────────────────────────────────────────────────────

DEFAULT_STATE = {
    "version":          2,
    "phase":            "preflight",
    "setup_done":       False,
    "sessions_complete": [],
    "last_commit":      None,
    "last_ci_url":      None,
    "paused_reason":    None,
    "started_at":       None,
    "updated_at":       None,
}

def load_state() -> dict:
    if BUILD_STATE_FILE.exists():
        try:
            return json.loads(BUILD_STATE_FILE.read_text())
        except Exception:
            pass
    return dict(DEFAULT_STATE)

def save_state(state: dict):
    state["updated_at"] = datetime.now(timezone.utc).isoformat()
    BUILD_STATE_FILE.write_text(json.dumps(state, indent=2))

def update_state(state: dict, **kwargs):
    state.update(kwargs)
    save_state(state)

def pause(state: dict, reason: str, detail: str = ""):
    msg = f"\n{'='*60}\n⏸  PAUSED — human input required\n{'='*60}"
    msg += f"\nReason: {reason}"
    if detail:
        msg += f"\n\n{textwrap.indent(detail, '  ')}"
    msg += f"\n\nTo resume:\n  python3 scripts/master_orchestrator.py\n"
    log(msg, "PAUSE")
    update_state(state, paused_reason=reason)
    sys.exit(2)


# ─── Git helpers ───────────────────────────────────────────────────────────────

def git(cmd: str, check: bool = True) -> str:
    r = subprocess.run(cmd, shell=True, capture_output=True, text=True, cwd=str(ROOT))
    if check and r.returncode != 0:
        raise RuntimeError(f"git failed: {cmd}\n{r.stderr.strip()}")
    return r.stdout.strip()

def current_commit() -> str:
    return git("git rev-parse --short HEAD")

def push_and_commit(message: str, branch: str = "develop") -> str:
    try:
        git("git add -A")
        git(f'git commit -m "{message}"')
    except RuntimeError:
        pass  # nothing to commit
    git(f"git push origin {branch}")
    sha = current_commit()
    log(f"Pushed to {branch}: {sha}")
    return sha


# ─── GitHub Actions CI polling ─────────────────────────────────────────────────

def get_latest_ci_run(branch: str) -> Optional[dict]:
    try:
        r = requests.get(f"{GH_API}/repos/{GITHUB_REPO}/actions/runs",
                         headers=GH_HEADERS,
                         params={"branch": branch, "per_page": 5, "event": "push"},
                         timeout=15)
        runs = r.json().get("workflow_runs", [])
        # Only watch the main CI workflow, ignore multi_agent and others
        for run in runs:
            if "Hali CI" in run.get("name", "") or run.get("path", "").endswith("ci.yml"):
                return run
        return runs[0] if runs else None
    except Exception as e:
        log(f"CI poll error: {e}", "WARN")
        return None

def wait_for_ci(branch: str, state: dict, attempt: int = 1) -> tuple[bool, str]:
    log(f"Waiting for CI on '{branch}' (attempt {attempt}/{CI_RETRY_MAX})...")
    deadline   = time.time() + CI_MAX_WAIT
    last_status = None

    while time.time() < deadline:
        run = get_latest_ci_run(branch)
        if not run:
            time.sleep(CI_POLL_SECS)
            continue

        status, conclusion = run.get("status"), run.get("conclusion")
        url = run.get("html_url", "")

        if status != last_status:
            log(f"  CI: {status}{' → ' + conclusion if conclusion else ''}")
            last_status = status

        if status == "completed":
            update_state(state, last_ci_url=url)
            passed = conclusion == "success"
            if not passed:
                log(f"  CI failed ({conclusion}): {url}", "WARN")
            return passed, url

        time.sleep(CI_POLL_SECS)

    return False, ""

def ci_with_retry(branch: str, state: dict) -> bool:
    for attempt in range(1, CI_RETRY_MAX + 1):
        passed, url = wait_for_ci(branch, state, attempt)
        if passed:
            return True
        if attempt < CI_RETRY_MAX:
            log(f"Retrying CI ({attempt}/{CI_RETRY_MAX})...")
            time.sleep(15)
            push_and_commit(f"ci: retry {attempt+1} [skip agent-run]", branch)
    pause(state,
          reason=f"CI failed after {CI_RETRY_MAX} attempts",
          detail=f"Review: {state.get('last_ci_url', 'unknown')}\n"
                 f"Fix the issue, push to {branch}, then re-run.")
    return False


# ─── Token extraction from Claude Code output ─────────────────────────────────

def parse_token_usage_from_output(output: str) -> tuple[int, int]:
    """
    Try to extract actual token counts from Claude Code output.
    Claude Code prints usage stats at the end of a session.
    Falls back to estimates if not found.
    """
    in_match  = re.search(r"input[_ ]tokens?[:\s]+([0-9,]+)", output, re.IGNORECASE)
    out_match = re.search(r"output[_ ]tokens?[:\s]+([0-9,]+)", output, re.IGNORECASE)
    if in_match and out_match:
        try:
            return (int(in_match.group(1).replace(",", "")),
                    int(out_match.group(1).replace(",", "")))
        except ValueError:
            pass
    return 0, 0   # caller falls back to estimate


# ─── Claude Code session runner ────────────────────────────────────────────────

def run_claude_code_session(session_num: str, session_file: str,
                             state: dict, dry_run: bool) -> bool:
    log_section(f"Session {session_num} — Claude Code")

    prior = sorted(state.get("sessions_complete", []))
    prefix = (f"Sessions 00 through {prior[-1]} are complete "
              f"(commit: {state.get('last_commit', 'unknown')}). "
              f"Begin Session {session_num}.\n\n") if prior else f"Begin Session {session_num}.\n\n"

    prompt = prefix + (ROOT / "session_prompts" / session_file).read_text()
    log(f"Prompt: {len(prompt):,} chars (~{len(prompt)//3:,} tokens)")

    out_dir = ROOT / "agent_outputs"
    out_dir.mkdir(parents=True, exist_ok=True)
    prompt_file = out_dir / f"session_{session_num}_prompt.txt"
    out_file    = out_dir / f"session_{session_num}_claude_output.txt"
    prompt_file.write_text(prompt)

    if dry_run:
        log(f"[DRY RUN] Would invoke claude for session {session_num}")
        return True

    shell_cmd = (f'claude --print --dangerously-skip-permissions '
                 f'--model claude-sonnet-4-6 '
                 f'"$(cat \'{prompt_file}\')"')

    t_start = time.time()
    try:
        result = subprocess.run(shell_cmd, shell=True, capture_output=True,
                                text=True, cwd=str(ROOT), timeout=CC_TIMEOUT,
                                env={**os.environ, "ANTHROPIC_API_KEY": ANTHROPIC_API_KEY})
    except subprocess.TimeoutExpired:
        log(f"Session {session_num} timed out after {CC_TIMEOUT//60}m", "ERROR")
        return False
    except FileNotFoundError:
        log("'claude' not found — npm install -g @anthropic-ai/claude-code", "ERROR")
        sys.exit(1)

    elapsed   = time.time() - t_start
    output    = result.stdout + ("\n[STDERR]\n" + result.stderr if result.stderr else "")
    out_file.write_text(output)

    completed = f"SESSION_{session_num}_COMPLETE" in output

    # Extract token usage from output, fall back to estimate
    in_tok, out_tok = parse_token_usage_from_output(output)
    if in_tok == 0:
        in_tok, out_tok = SESSION_TOKEN_ESTIMATES.get(session_num, (60_000, 7_000))
        log(f"Token usage not found in output — using estimate ({in_tok:,}in/{out_tok:,}out)")

    actual_cost = record_session_spend(session_num, in_tok, out_tok, "claude-code")
    log(f"Session {session_num} {'✓ complete' if completed else '✗ incomplete'} "
        f"in {elapsed/60:.1f}m | Cost: ${actual_cost:.4f} | "
        f"Tokens: {in_tok:,}in/{out_tok:,}out")

    if not completed:
        log(f"Last 500 chars:\n{output[-500:]}", "WARN")

    return completed


# ─── Multi-agent session runner ────────────────────────────────────────────────

def run_multi_agent_session(session_num: str, state: dict, dry_run: bool) -> bool:
    log_section(f"Session {session_num} — Multi-Agent (A+B+C+D)")
    cmd = f"python3 scripts/orchestrate.py --session {session_num}"
    if dry_run:
        cmd += " --dry-run"
    result = subprocess.run(cmd, shell=True, cwd=str(ROOT),
                             env={**os.environ,
                                  "ANTHROPIC_API_KEY": ANTHROPIC_API_KEY,
                                  "GITHUB_TOKEN": GITHUB_TOKEN,
                                  "GITHUB_REPO": GITHUB_REPO,
                                  "MONTHLY_API_BUDGET_USD": str(MONTHLY_BUDGET)})
    if result.returncode == 0:
        log(f"Multi-agent session {session_num} approved.")
        return True
    elif result.returncode == 1:
        pause(state,
              reason=f"Agent C rejected session {session_num}",
              detail=f"Fix BLOCKING items in agent_outputs/session_{session_num}/agent_c.md\n"
                     f"Then: python3 scripts/master_orchestrator.py --from-session {session_num}")
    log(f"Orchestrator exit code: {result.returncode}", "ERROR")
    return False


# ─── Setup phase ───────────────────────────────────────────────────────────────

def run_setup(state: dict, dry_run: bool):
    log_section("Setup Phase")

    # Initialise spend file with current budget
    if not SPEND_FILE.exists():
        save_spend({"total_usd": 0.0, "budget_usd": MONTHLY_BUDGET,
                    "remaining_usd": MONTHLY_BUDGET, "sessions": {}})
        log(f"Spend tracker initialised: ${MONTHLY_BUDGET:.2f} budget")
    else:
        # Update budget from env in case it changed
        data = load_spend()
        data["budget_usd"] = MONTHLY_BUDGET
        save_spend(data)

    for label, cmd in [
        ("Branch protection", "python3 scripts/protect_branches.py"),
        ("GitHub issues",     "python3 scripts/create_github_issues.py"),
    ]:
        log(f"  {label}...")
        if not dry_run:
            r = subprocess.run(cmd, shell=True, cwd=str(ROOT),
                                env={**os.environ, "GITHUB_TOKEN": GITHUB_TOKEN,
                                     "GITHUB_REPO": GITHUB_REPO,
                                     "GITHUB_OWNER_LOGIN": GITHUB_OWNER})
            if r.returncode != 0:
                log(f"  {label} returned non-zero — may already exist, continuing", "WARN")
        else:
            log(f"  [DRY RUN] Would run: {cmd}")

    if not dry_run:
        try:
            git("git checkout develop 2>/dev/null || git checkout -b develop")
            git("git push origin develop 2>/dev/null || true")
            git("git checkout main")
        except RuntimeError:
            pass

    update_state(state, setup_done=True, phase="sessions")
    log("Setup complete.")


# ─── Preflight checks ──────────────────────────────────────────────────────────

def preflight_checks(state: dict, mode: str, dry_run: bool):
    log_section("Preflight Checks")
    errors = []

    for var in ["ANTHROPIC_API_KEY", "GITHUB_TOKEN", "GITHUB_REPO", "GITHUB_OWNER_LOGIN"]:
        if not os.environ.get(var):
            errors.append(f"Missing: {var}")

    if not (ROOT / ".env").exists():
        errors.append(".env not found — copy .env.example to .env")

    if mode == "claude-code":
        r = subprocess.run("claude --version", shell=True, capture_output=True)
        if r.returncode != 0:
            errors.append("Claude Code not installed: npm install -g @anthropic-ai/claude-code")

    r = subprocess.run("git config user.email", shell=True, capture_output=True,
                       text=True, cwd=str(ROOT))
    if not r.stdout.strip():
        errors.append("git user.email not set: git config --global user.email 'you@example.com'")

    r = subprocess.run("docker info", shell=True, capture_output=True)
    if r.returncode != 0:
        log("Docker not running — start Docker Desktop (needed for local DB)", "WARN")

    if errors:
        for e in errors: log(f"  ✗ {e}", "ERROR")
        pause(state, reason="Preflight failed", detail="\n".join(errors))

    log("Preflight checks passed.")

    # Show budget at start of every run
    bs = get_budget_status()
    log(f"Budget: ${bs['spent']:.4f} spent / ${bs['budget']:.2f} total "
        f"(${bs['remaining']:.4f} remaining, {bs['pct_left']*100:.1f}%)")


# ─── Status display ────────────────────────────────────────────────────────────

def print_status(state: dict):
    done    = set(state.get("sessions_complete", []))
    pending = set(SESSIONS.keys()) - done

    print(f"\n{'═'*56}")
    print(f"  Hali Build State")
    print(f"{'═'*56}")
    print(f"  Phase:   {state.get('phase', 'unknown')}")
    print(f"  Setup:   {'✓' if state.get('setup_done') else '·'}")
    print(f"  Progress: {len(done)}/8 sessions complete")
    print()
    bs = get_budget_status()
    for n, (_, name) in SESSIONS.items():
        mark = "✓" if n in done else "·"
        actual = bs["sessions"].get(n)
        cost_str = f"  ${actual['cost_usd']:.4f}" if actual else ""
        print(f"    {mark}  {n} — {name}{cost_str}")
    if state.get("paused_reason"):
        print(f"\n  ⏸  Paused: {state['paused_reason']}")
    print()
    print(f"  Budget:  ${bs['spent']:.4f} spent / ${bs['budget']:.2f}")
    print(f"           {budget_bar(bs['pct_used'], width=24)}")
    if state.get("last_ci_url"):
        print(f"  Last CI: {state['last_ci_url']}")
    print(f"{'═'*56}\n")


# ─── Main pipeline ─────────────────────────────────────────────────────────────

def run(from_session: str = None, dry_run: bool = False, mode: str = "claude-code"):
    state = load_state()

    if state.get("paused_reason"):
        log(f"Resuming from: {state['paused_reason']}", "WARN")
        state["paused_reason"] = None
        save_state(state)

    if not state.get("started_at"):
        state["started_at"] = datetime.now(timezone.utc).isoformat()
        save_state(state)

    log_section(f"Hali Master Orchestrator  mode={mode}  dry={dry_run}")
    preflight_checks(state, mode, dry_run)

    if not state.get("setup_done"):
        run_setup(state, dry_run)
    else:
        log("Setup already done — skipping.")

    sessions_done = set(state.get("sessions_complete", []))

    # Dry run: show full cost forecast and exit
    if dry_run:
        print_cost_dashboard(sessions_done)
        pending_total, breakdown = forecast_remaining_sessions(sessions_done)
        print(f"Dry run complete. Estimated remaining cost: ~${pending_total:.4f}")
        return

    start_from = from_session or next((n for n in SESSIONS if n not in sessions_done), None)
    if start_from is None:
        log("All 8 sessions complete.")
        update_state(state, phase="done")
        print_status(state)
        print_cost_dashboard(sessions_done)
        return

    for session_num, (session_file, phase_name) in SESSIONS.items():
        if session_num < start_from:
            continue
        if session_num in sessions_done:
            log(f"Session {session_num} ({phase_name}) already done — skipping.")
            continue

        # ── Budget gate ────────────────────────────────────────────────────
        budget_gate(session_num, state, dry_run, mode)

        update_state(state, phase=f"session_{session_num}")
        branch = f"session/{session_num}-{phase_name}"

        # Create session branch
        try:
            git(f"git checkout -b {branch}")
        except RuntimeError:
            git(f"git checkout {branch}")

        # Run the session
        t0 = time.time()
        if mode == "multi-agent":
            success = run_multi_agent_session(session_num, state, dry_run)
        else:
            success = run_claude_code_session(session_num, session_file, state, dry_run)

        if not success:
            pause(state,
                  reason=f"Session {session_num} incomplete",
                  detail=f"Output: agent_outputs/session_{session_num}_claude_output.txt\n"
                         f"Resume: python3 scripts/master_orchestrator.py --from-session {session_num}")

        # Commit, push, wait for CI
        sha = push_and_commit(f"build: session {session_num} {phase_name}", branch)
        update_state(state, last_commit=sha)

        if not ci_with_retry(branch, state):
            return   # pause() called inside ci_with_retry

        # Merge to develop
        log(f"Merging {branch} → develop...")
        resp = requests.put(f"{GH_API}/repos/{GITHUB_REPO}/merges", headers=GH_HEADERS,
                            json={"base": "develop", "head": branch,
                                  "commit_message": f"build: session {session_num} {phase_name}"})
        if resp.status_code not in (201, 204):
            log(f"Merge response {resp.status_code} — may need manual merge", "WARN")

        sessions_done.add(session_num)
        update_state(state, sessions_complete=sorted(sessions_done))

        # Post-session cost summary
        bs = get_budget_status()
        log(f"Session {session_num} done | "
            f"Cumulative: ${bs['spent']:.4f} | "
            f"Remaining: ${bs['remaining']:.4f} ({bs['pct_left']*100:.1f}%)")

        git("git checkout develop")

    # ── Final merge to main ────────────────────────────────────────────────
    log_section("All Sessions Complete — Merging to Main")
    update_state(state, phase="done")
    push_and_commit("build: Hali MVP complete — merging to main", "develop")
    resp = requests.put(f"{GH_API}/repos/{GITHUB_REPO}/merges", headers=GH_HEADERS,
                        json={"base": "main", "head": "develop",
                              "commit_message": "build: Hali v1.0"})
    if resp.status_code in (201, 204):
        log("develop → main merged. Approve the production deployment in GitHub Actions.")
    else:
        log(f"Merge returned {resp.status_code} — open a PR from develop to main manually.", "WARN")

    print_status(state)
    print_cost_dashboard(sessions_done)


# ─── Entry point ───────────────────────────────────────────────────────────────

if __name__ == "__main__":
    p = argparse.ArgumentParser(description="Hali Master Orchestrator",
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--from-session", metavar="NN", help="Resume from session 00–07")
    p.add_argument("--dry-run",  action="store_true", help="Estimate costs without running")
    p.add_argument("--status",   action="store_true", help="Show state and exit")
    p.add_argument("--costs",    action="store_true", help="Show cost dashboard and exit")
    p.add_argument("--mode",     choices=["claude-code", "multi-agent"], default="claude-code")
    p.add_argument("--budget",   type=float, help="Override monthly budget for this run")
    p.add_argument("--reset",    action="store_true", help="Reset build state (keeps spend log)")
    args = p.parse_args()

    if args.budget:
        os.environ["MONTHLY_API_BUDGET_USD"] = str(args.budget)
        MONTHLY_BUDGET = args.budget

    if args.reset:
        BUILD_STATE_FILE.unlink(missing_ok=True)
        print("Build state reset. Spend log preserved.")
        sys.exit(0)

    state = load_state()

    if args.status:
        print_status(state)
        sys.exit(0)

    if args.costs:
        print_cost_dashboard(set(state.get("sessions_complete", [])))
        sys.exit(0)

    run(from_session=args.from_session, dry_run=args.dry_run, mode=args.mode)

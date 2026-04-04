#!/usr/bin/env python3
"""
Hali — Agent C Full-Codebase Validation
Runs a comprehensive architecture + correctness review across all 8 build sessions.

Usage:
    python3 scripts/agent_c_full_validation.py
    python3 scripts/agent_c_full_validation.py --dry-run   # estimate cost only
    python3 scripts/agent_c_full_validation.py --depth deep  # include full source files

Output:
    agent_outputs/full_validation_report.md
"""

import os, sys, time, argparse
from pathlib import Path
from datetime import datetime, timezone

try:
    import anthropic
except ImportError:
    print("Run: pip install anthropic")
    sys.exit(1)

ROOT        = Path(__file__).parent.parent
OUTPUT_FILE = ROOT / "agent_outputs" / "full_validation_report.md"

# Sonnet 4.6 pricing
INPUT_COST_PER_MTOK  = 3.00
OUTPUT_COST_PER_MTOK = 15.00
MAX_OUTPUT_TOKENS    = 8192

ANTHROPIC_API_KEY = os.environ.get("ANTHROPIC_API_KEY", "")


# ─── File readers ──────────────────────────────────────────────────────────────

def read(path: Path, max_chars: int = 0) -> str:
    try:
        text = path.read_text(errors="replace")
        if max_chars and len(text) > max_chars:
            return text[:max_chars] + f"\n... [truncated at {max_chars} chars]"
        return text
    except Exception:
        return f"[Could not read: {path}]"

def find_cs_files(directory: Path, max_files: int = 80) -> list[Path]:
    """Find .cs files, prioritising service/domain/controller files over migrations."""
    all_files = list(directory.rglob("*.cs")) if directory.exists() else []
    # Deprioritise migrations and model snapshots
    priority = [f for f in all_files if "Migration" not in f.name and "Snapshot" not in f.name]
    migrations = [f for f in all_files if "Migration" in f.name or "Snapshot" in f.name]
    ordered = priority + migrations
    return ordered[:max_files]

def summarise_cs_files(files: list[Path], max_chars_each: int = 3000) -> str:
    """Read .cs files and produce a summarised source listing."""
    parts = []
    for f in files:
        rel = f.relative_to(ROOT) if ROOT in f.parents else f
        content = read(f, max_chars_each)
        parts.append(f"### {rel}\n```csharp\n{content}\n```")
    return "\n\n".join(parts)


# ─── Context assembly ──────────────────────────────────────────────────────────

def build_context(depth: str = "standard") -> tuple[str, str]:
    """
    Assemble the full Agent C context.
    Returns (system_prompt, user_message).
    depth: "standard" = summaries + key files
           "deep"     = full source listing
    """

    # ── Spec docs ──────────────────────────────────────────────────────────────
    spec_docs = {
        "CLAUDE.md (engineering doctrine)":         ROOT / "CLAUDE.md",
        "mvp_locked_decisions.md":                  ROOT / "mvp_locked_decisions.md",
        "02_openapi.yaml":                          ROOT / "02_openapi.yaml",
        "01_postgres_schema.sql":                   ROOT / "01_postgres_schema.sql",
        "nlp_extraction_prompt.md":                 ROOT / "nlp_extraction_prompt.md",
        "mobile_screen_inventory.md":               ROOT / "mobile_screen_inventory.md",
    }

    spec_section = "# SPEC DOCUMENTS\n\n"
    for label, path in spec_docs.items():
        spec_section += f"## {label}\n{read(path, 8000)}\n\n"

    # ── Session prompts (what each session was asked to build) ─────────────────
    session_prompts_section = "# SESSION PROMPTS (what each session was instructed to build)\n\n"
    for n in range(8):
        p = ROOT / "session_prompts" / f"session_0{n}_*.md"
        matches = list(ROOT.glob(f"session_prompts/session_0{n}_*.md"))
        if matches:
            session_prompts_section += f"## Session 0{n}\n{read(matches[0], 4000)}\n\n"

    # ── Session outputs (what Claude Code actually reported building) ──────────
    session_outputs_section = "# SESSION COMPLETION REPORTS (what Claude Code reported building)\n\n"
    for n in range(8):
        out = ROOT / "agent_outputs" / f"session_0{n}_claude_output.txt"
        if out.exists():
            # Take last 3000 chars — the summary is always at the end
            text = out.read_text(errors="replace")
            tail = text[-3000:] if len(text) > 3000 else text
            session_outputs_section += f"## Session 0{n} output (last 3000 chars)\n{tail}\n\n"
        else:
            session_outputs_section += f"## Session 0{n}\n[Output file not found — session may not have run yet]\n\n"

    # ── Source code ───────────────────────────────────────────────────────────
    source_section = "# ACTUAL SOURCE CODE\n\n"

    src_dir = ROOT / "src"
    tests_dir = ROOT / "tests"

    if depth == "deep":
        # Full source listing
        cs_files = find_cs_files(src_dir, max_files=80)
        source_section += f"## src/ ({len(cs_files)} files)\n\n"
        source_section += summarise_cs_files(cs_files, max_chars_each=4000)
        test_files = find_cs_files(tests_dir, max_files=40)
        source_section += f"\n\n## tests/ ({len(test_files)} files)\n\n"
        source_section += summarise_cs_files(test_files, max_chars_each=3000)
    else:
        # Standard: key files only — one per module
        key_patterns = [
            "*/Services/*Service.cs",
            "*/Repositories/*Repository.cs",
            "*/Controllers/*Controller.cs",
            "*/Domain/Entities/*.cs",
            "*/Domain/Services/*.cs",
            "*/Workers/*.cs",
            "*/Civis/*.cs",
            "*/Nlp/*.cs",
        ]
        found = []
        for pattern in key_patterns:
            found.extend(list(src_dir.glob(pattern))[:5])

        # Deduplicate and limit
        seen = set()
        unique = []
        for f in found:
            if str(f) not in seen:
                seen.add(str(f))
                unique.append(f)

        if unique:
            source_section += f"## Key source files ({len(unique)} files)\n\n"
            source_section += summarise_cs_files(unique[:60], max_chars_each=3000)
        else:
            # Fallback: just show the directory structure
            source_section += "## Source directory structure\n```\n"
            for p in sorted(src_dir.rglob("*.cs"))[:100] if src_dir.exists() else []:
                source_section += str(p.relative_to(ROOT)) + "\n"
            source_section += "```\n"
            source_section += "\n(Source files found but pattern matching yielded no results — showing structure only)\n"

        # Always include test files
        if tests_dir.exists():
            test_files = find_cs_files(tests_dir, max_files=30)
            if test_files:
                source_section += f"\n\n## Test files ({len(test_files)} files)\n\n"
                source_section += summarise_cs_files(test_files, max_chars_each=2000)

    # ── Agent C system prompt ─────────────────────────────────────────────────
    agent_c_prompt = read(ROOT / "agent_prompts" / "agent_c_validator.md")

    system = f"""{agent_c_prompt}

---

# FULL-CODEBASE VALIDATION CONTEXT

This is a FULL-CODEBASE validation pass covering all 8 build sessions (00-07).
You are not validating a single session — you are validating the entire MVP.

Your job:
1. Check every module against the spec docs and locked decisions
2. Identify BLOCKING issues that must be fixed before production
3. Identify WARNINGS that should be addressed
4. Identify anything built OUTSIDE MVP scope
5. Confirm the vertical slice works end-to-end (signal → cluster → participation → restoration)
6. Produce a comprehensive validation report

Be thorough but fair. If source files are missing from the context, note it but do not
assume they weren't built — Claude Code may have committed files not shown here.
"""

    user = f"""{spec_section}

---

{session_prompts_section}

---

{session_outputs_section}

---

{source_section}

---

# YOUR TASK

Produce a FULL-CODEBASE validation report covering all 8 sessions.

Structure your output as follows:

## EXECUTIVE SUMMARY
Overall verdict: PASS | PASS_WITH_NOTES | FAIL
Total BLOCKING issues: N
Total WARNINGS: N
Sessions with concerns: list

## PER-SESSION ANALYSIS
For each session (00-07), briefly confirm what was built and flag any issues.

## BLOCKING ISSUES
List every blocking issue with:
- Session/Module
- File (if known)
- Description
- What needs to be fixed

## WARNINGS
List every warning (non-blocking but should be addressed).

## SPEC DRIFT
Any implementation that diverges from CLAUDE.md, mvp_locked_decisions.md, or OpenAPI spec.

## OUT-OF-SCOPE ITEMS
Anything built that wasn't in the MVP scope.

## VERTICAL SLICE ASSESSMENT
Can the core flow (signal preview → submit → cluster activation → participation → restoration) run end-to-end?
What's missing or broken?

## MOBILE APP STATUS
What exists for the React Native app? What's missing?

## RECOMMENDED ACTIONS BEFORE PRODUCTION
Ordered by priority.

## AGENT_C_LESSONS:
Phase: full-codebase
Session: all

[Extract the most important lessons for future Agent A sessions]
"""

    return system, user


# ─── Cost estimation ───────────────────────────────────────────────────────────

def estimate_cost(system: str, user: str) -> tuple[float, int, int]:
    total_chars = len(system) + len(user)
    in_tok = total_chars // 3   # ~3 chars per token
    out_tok = MAX_OUTPUT_TOKENS
    cost = (in_tok / 1_000_000 * INPUT_COST_PER_MTOK +
            out_tok / 1_000_000 * OUTPUT_COST_PER_MTOK)
    return round(cost, 4), in_tok, out_tok


# ─── Main ─────────────────────────────────────────────────────────────────────

def run(dry_run: bool = False, depth: str = "standard"):
    print(f"\n{'='*60}")
    print(f"  Hali — Agent C Full-Codebase Validation")
    print(f"  Depth: {depth} | Dry run: {dry_run}")
    print(f"{'='*60}\n")

    print("Assembling context...")
    t0 = time.time()
    system, user = build_context(depth)
    elapsed = time.time() - t0

    cost, in_tok, out_tok = estimate_cost(system, user)

    print(f"Context assembled in {elapsed:.1f}s")
    print(f"Input:  {in_tok:,} tokens (~{len(system)+len(user):,} chars)")
    print(f"Output: {out_tok:,} tokens (max)")
    print(f"Cost:   ~${cost:.4f}")

    if dry_run:
        print("\n[DRY RUN] No API call made.")
        # Save context to file for inspection
        preview = ROOT / "agent_outputs" / "agent_c_context_preview.txt"
        preview.parent.mkdir(parents=True, exist_ok=True)
        preview.write_text(f"SYSTEM ({len(system):,} chars):\n{system[:2000]}...\n\nUSER ({len(user):,} chars):\n{user[:2000]}...")
        print(f"Context preview saved to: {preview}")
        return

    if not ANTHROPIC_API_KEY:
        print("ERROR: ANTHROPIC_API_KEY not set")
        sys.exit(1)

    # Check context limit
    if in_tok > 180_000:
        print(f"WARNING: {in_tok:,} tokens is very large. Consider --depth standard")
        answer = input("Continue anyway? [yes/no]: ").strip().lower()
        if answer != "yes":
            sys.exit(0)

    print("\nCalling Agent C...")
    client = anthropic.Anthropic(api_key=ANTHROPIC_API_KEY)

    t_start = time.time()
    try:
        response = client.messages.create(
            model="claude-sonnet-4-6",
            max_tokens=MAX_OUTPUT_TOKENS,
            messages=[{"role": "user", "content": user}],
            system=system,
        )
    except Exception as e:
        print(f"API error: {e}")
        sys.exit(1)

    elapsed = time.time() - t_start
    output = response.content[0].text

    actual_in  = response.usage.input_tokens
    actual_out = response.usage.output_tokens
    actual_cost = (actual_in / 1_000_000 * INPUT_COST_PER_MTOK +
                   actual_out / 1_000_000 * OUTPUT_COST_PER_MTOK)

    print(f"\nAgent C completed in {elapsed:.1f}s")
    print(f"Actual tokens: {actual_in:,} in / {actual_out:,} out")
    print(f"Actual cost:   ${actual_cost:.4f}")

    # Save report
    OUTPUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    report = f"""# Hali — Agent C Full-Codebase Validation Report
Generated: {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}
Depth: {depth}
Tokens: {actual_in:,} in / {actual_out:,} out
Cost: ${actual_cost:.4f}

---

{output}
"""
    OUTPUT_FILE.write_text(report)
    print(f"\nReport saved to: {OUTPUT_FILE}")

    # Print executive summary
    lines = output.split("\n")
    in_summary = False
    summary_lines = []
    for line in lines:
        if "EXECUTIVE SUMMARY" in line.upper():
            in_summary = True
        elif in_summary and line.startswith("## ") and "EXECUTIVE" not in line.upper():
            break
        if in_summary:
            summary_lines.append(line)

    if summary_lines:
        print(f"\n{'─'*56}")
        print("\n".join(summary_lines[:15]))
        print(f"{'─'*56}")

    print(f"\nFull report: {OUTPUT_FILE}")
    print("Open it with: code agent_outputs/full_validation_report.md")


if __name__ == "__main__":
    p = argparse.ArgumentParser(description="Agent C full-codebase validation")
    p.add_argument("--dry-run", action="store_true",
                   help="Estimate cost without calling the API")
    p.add_argument("--depth", choices=["standard", "deep"], default="standard",
                   help="standard = key files only (cheaper), deep = full source listing")
    args = p.parse_args()
    run(dry_run=args.dry_run, depth=args.depth)

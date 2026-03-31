#!/usr/bin/env python3
"""
Hali MVP — Post-CI Spec Verifier (Item 21)
Called after CI completes. Reads actual test results and coverage, then
invokes Agent C to analyse what failed and appends lessons to agent_a_lessons.md.

This closes the loop between CI failures and Agent A's self-learning system.
"""

import os, sys, json, glob
from pathlib import Path
from datetime import datetime, timezone
from anthropic import Anthropic
from tenacity import retry, stop_after_attempt, wait_exponential, retry_if_exception_type
from anthropic import APIStatusError, APIConnectionError

ANTHROPIC_API_KEY = os.environ.get("ANTHROPIC_API_KEY")
CI_CONCLUSION     = os.environ.get("CI_CONCLUSION", "unknown")
CI_RUN_URL        = os.environ.get("CI_RUN_URL", "")
MODEL             = "claude-sonnet-4-6"
LESSONS_FILE      = "agent_prompts/agent_a_lessons.md"
LESSONS_MARKER    = "<!-- LESSONS_APPEND_MARKER — do not remove this line, orchestrator appends below it -->"
ROOT              = Path(__file__).parent.parent


def read_file(p): return (ROOT / p).read_text() if (ROOT / p).exists() else ""

def read_trx_summary(glob_pattern: str) -> str:
    """Extract pass/fail counts from .trx test result files."""
    files = glob.glob(str(ROOT / glob_pattern))
    if not files:
        return "No test result files found."
    summaries = []
    for f in files[:5]:
        try:
            content = open(f).read()
            # Very rough TRX parsing — extract outcome counts
            import re
            outcomes = re.findall(r'outcome="(\w+)"', content)
            passed  = outcomes.count("Passed")
            failed  = outcomes.count("Failed")
            skipped = outcomes.count("NotExecuted")
            summaries.append(f"{Path(f).name}: {passed} passed, {failed} failed, {skipped} skipped")
        except Exception as e:
            summaries.append(f"{Path(f).name}: could not parse ({e})")
    return "\n".join(summaries)

def read_coverage_summary() -> str:
    """Read the plain-text coverage summary if it exists."""
    summary_path = ROOT / "coverage-report/Summary.txt"
    if summary_path.exists():
        return summary_path.read_text()[:1000]
    # Try ci-artifacts directory
    for f in glob.glob(str(ROOT / "ci-artifacts/**/Summary.txt"), recursive=True):
        return open(f).read()[:1000]
    return "Coverage summary not found in artifacts."

@retry(stop=stop_after_attempt(3), wait=wait_exponential(min=4, max=30),
       retry=retry_if_exception_type((APIStatusError, APIConnectionError)))
def call_claude(system, user):
    r = Anthropic(api_key=ANTHROPIC_API_KEY).messages.create(
        model=MODEL, max_tokens=4096, system=system,
        messages=[{"role": "user", "content": user}])
    return r.content[0].text

def append_lessons(lessons_text: str, source: str):
    path = ROOT / LESSONS_FILE
    if not path.exists():
        print(f"Lessons file not found: {LESSONS_FILE}")
        return
    current = path.read_text()
    date    = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    entry   = (
        f"\n## Post-CI Verification — {source} ({date})\n"
        f"_Source: {CI_RUN_URL}_\n\n"
        f"{lessons_text}\n"
    )
    updated = (current.replace(LESSONS_MARKER, entry + "\n" + LESSONS_MARKER)
               if LESSONS_MARKER in current else current + "\n" + entry)
    path.write_text(updated)
    print(f"Lessons updated in {LESSONS_FILE}")

def run():
    print(f"Post-CI Spec Verifier — CI conclusion: {CI_CONCLUSION}")

    test_summary  = read_trx_summary("ci-artifacts/**/*.trx")
    cov_summary   = read_coverage_summary()
    claude_md     = read_file("claude.md")[:3000]
    locked        = read_file("mvp_locked_decisions.md")[:2000]

    system = """You are Agent C (Spec Verifier), operating post-CI.
You analyse actual CI test results and coverage reports to determine:
1. Whether any failures indicate an architectural mistake by Agent A
2. Whether coverage below 95% indicates missing test cases by Agent B
3. What lessons Agent A should learn to avoid repeating these failures

Output ONLY a AGENT_C_LESSONS block if you find real issues.
Output "No new lessons — CI passed cleanly." if everything is fine.

Format lessons as:
AGENT_C_LESSONS:
Source: post-CI verification

LESSON N:
Category: [Architecture|Coverage|Contracts|CIVIS|Auth|Business-Rules|Other]
Mistake: <one sentence>
Correct: <one or two sentences>
Example:
  WRONG:  ...
  RIGHT:  ...
"""

    user = f"""
## CI Result
Conclusion: {CI_CONCLUSION}
Run URL: {CI_RUN_URL}

## Test Results
{test_summary}

## Coverage Report
{cov_summary}

## Project context
### claude.md (excerpt)
{claude_md}

### mvp_locked_decisions.md (excerpt)
{locked}

Based on these CI results, what should Agent A learn for future sessions?
If CI passed and coverage >= 95%, output: "No new lessons — CI passed cleanly."
"""

    print("Calling Agent C for post-CI analysis...")
    response = call_claude(system, user)
    print(f"Response received ({len(response)} chars)")

    if "No new lessons" in response:
        print("CI clean — no lessons to add.")
        return

    if "AGENT_C_LESSONS:" in response:
        append_lessons(response, f"CI {CI_CONCLUSION}")
        print("Lessons appended from CI verification.")
    else:
        print("No structured lessons block found in response — skipping.")
        print(f"Response preview: {response[:300]}")

if __name__ == "__main__":
    run()

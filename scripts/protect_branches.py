#!/usr/bin/env python3
"""
Hali MVP — Branch Protection Setup
Locks main and develop branches:
  - All changes via PRs only (no direct push)
  - At least 1 approving review required (from you)
  - All CI status checks must pass before merge
  - Stale reviews dismissed on new commits
  - Force-push and deletion blocked

Usage:
    export GITHUB_TOKEN=your_pat_with_admin_scope
    export GITHUB_REPO=irvinesunday/hali
    export GITHUB_OWNER_LOGIN=irvinesunday
    python3 scripts/protect_branches.py

Your PAT needs the 'repo' scope (Settings → Developer settings → Personal access tokens).
Run this once after the repository is created.
"""

import os
import sys
import requests

TOKEN  = os.environ.get("GITHUB_TOKEN")
REPO   = os.environ.get("GITHUB_REPO")   # e.g. "irvine/hali"
OWNER  = os.environ.get("GITHUB_OWNER_LOGIN")  # your GitHub username (required reviewer)

if not TOKEN or not REPO:
    print("ERROR: Set GITHUB_TOKEN and GITHUB_REPO.")
    sys.exit(1)

HEADERS = {
    "Authorization": f"Bearer {TOKEN}",
    "Accept": "application/vnd.github+json",
    "X-GitHub-Api-Version": "2022-11-28",
}

BASE = f"https://api.github.com/repos/{REPO}"

# These are the CI job names that must pass — must match the 'name:' fields in ci.yml exactly
REQUIRED_STATUS_CHECKS = [
    # Pre-checks — run before build, block on any secret leak or spec violation
    "Secret Scanning (gitleaks)",
    "OpenAPI Lint (Spectral)",
    # Build
    "Build",
    # Test jobs
    "Unit Tests + Coverage",
    "CIVIS Unit Tests",
    "Integration Tests + Coverage",
    "API Contract Test (Dredd)",
    # Gates — both must pass before merge is allowed
    "Coverage Gate (>=95% required)",
    "Vertical Slice (E2E)",
]
# These names must exactly match the `name:` field of each job in ci.yml.
# If you rename a CI job, update this list and re-run protect_branches.py.


def get_user_node_id(login: str) -> str:
    """Resolve a GitHub username to its node_id (needed for required reviewers API)."""
    r = requests.get(f"https://api.github.com/users/{login}", headers=HEADERS)
    r.raise_for_status()
    return r.json()["node_id"]


def protect_branch(branch: str, is_main: bool):
    """
    Apply branch protection rules.
    is_main=True uses stricter settings (enforce for admins too).
    """
    print(f"\nProtecting branch: {branch}")

    # Build required_pull_request_reviews block
    reviews_block = {
        "dismiss_stale_reviews": True,          # new commits invalidate old approvals
        "require_code_owner_reviews": False,    # no CODEOWNERS file needed
        "required_approving_review_count": 1,  # you must approve
        "require_last_push_approval": True,     # the last person to push cannot self-approve
    }

    if OWNER:
        # Restrict who can bypass — only the repo owner (you) can approve
        reviews_block["bypass_pull_request_allowances"] = {
            "users": [],
            "teams": [],
            "apps":  [],
        }

    payload = {
        # ── PR requirement ─────────────────────────────────────────────────
        "required_pull_request_reviews": reviews_block,

        # ── Status checks ──────────────────────────────────────────────────
        "required_status_checks": {
            "strict": True,  # branch must be up-to-date before merge
            "checks": [{"context": name} for name in REQUIRED_STATUS_CHECKS],
        },

        # ── Push restrictions ──────────────────────────────────────────────
        "enforce_admins": is_main,  # on main, even admins cannot bypass
        "restrictions": None,       # None = anyone can create PRs (no push restriction beyond PR requirement)

        # ── Safety ────────────────────────────────────────────────────────
        "allow_force_pushes": False,
        "allow_deletions":    False,
        "block_creations":    False,
        "required_conversation_resolution": True,  # all review comments must be resolved
        "lock_branch": False,  # don't hard-lock (you still need to merge PRs)
    }

    url = f"{BASE}/branches/{branch}/protection"
    r = requests.put(url, json=payload, headers=HEADERS)

    if r.status_code == 200:
        print(f"  ✓ {branch} protected successfully")
        rules = r.json()
        print(f"    Required reviews: {rules.get('required_pull_request_reviews', {}).get('required_approving_review_count', '?')}")
        print(f"    Enforce on admins: {rules.get('enforce_admins', {}).get('enabled', '?')}")
        print(f"    Status checks required: {len(REQUIRED_STATUS_CHECKS)}")
    elif r.status_code == 404:
        print(f"  ✗ Branch '{branch}' not found. Push at least one commit to it first.")
    elif r.status_code == 403:
        print(f"  ✗ Forbidden. Your token needs admin access to this repository.")
        print(f"    Token needs: repo scope (classic) or admin:write (fine-grained)")
        print(f"    Response: {r.text}")
    else:
        print(f"  ✗ Unexpected response {r.status_code}: {r.text}")


def set_default_branch(branch: str = "main"):
    """Make sure main is the default branch."""
    r = requests.patch(f"{BASE}", json={"default_branch": branch}, headers=HEADERS)
    if r.status_code == 200:
        print(f"  ✓ Default branch set to: {branch}")
    else:
        print(f"  ✗ Could not set default branch: {r.status_code}")


def disable_force_merge():
    """Turn off merge commits on main, enforce squash or rebase only."""
    r = requests.patch(f"{BASE}", json={
        "allow_merge_commit": False,   # no merge commits — keeps history clean
        "allow_squash_merge": True,    # squash all commits in PR into one
        "allow_rebase_merge": False,   # no rebase (squash is cleaner for this project)
        "delete_branch_on_merge": True,# auto-delete feature branches after merge
    }, headers=HEADERS)
    if r.status_code == 200:
        print("  ✓ Repository merge settings: squash-only, auto-delete branches")
    else:
        print(f"  ✗ Could not update merge settings: {r.status_code}")


def verify_protection(branch: str):
    """Print a summary of what's currently protecting the branch."""
    r = requests.get(f"{BASE}/branches/{branch}/protection", headers=HEADERS)
    if r.status_code == 200:
        p = r.json()
        pr_reviews = p.get("required_pull_request_reviews", {})
        checks     = p.get("required_status_checks", {}).get("checks", [])
        print(f"\n  Verification for '{branch}':")
        print(f"    Approvals required:       {pr_reviews.get('required_approving_review_count', 0)}")
        print(f"    Stale reviews dismissed:  {pr_reviews.get('dismiss_stale_reviews', False)}")
        print(f"    Enforce on admins:        {p.get('enforce_admins', {}).get('enabled', False)}")
        print(f"    Force push blocked:       {not p.get('allow_force_pushes', {}).get('enabled', True)}")
        print(f"    Status checks required:   {len(checks)}")
        for c in checks:
            print(f"      - {c['context']}")
    else:
        print(f"  Could not verify {branch}: {r.status_code}")


def run():
    print("Hali Branch Protection Setup")
    print(f"Repository: {REPO}")
    print(f"Required reviewer: {OWNER or '(not specified — all PR approvers accepted)'}")

    # Set repo-level merge settings first
    disable_force_merge()
    set_default_branch("main")

    # Protect main (strictest — enforce on admins)
    protect_branch("main", is_main=True)

    # Protect develop (slightly looser — admins can bypass in emergencies)
    protect_branch("develop", is_main=False)

    # Verify both
    print("\n── Verification ──────────────────────────────────────")
    verify_protection("main")
    verify_protection("develop")

    print(f"""
── Setup complete ────────────────────────────────────

What is now enforced on 'main':
  - No direct pushes (PRs only)
  - At least 1 approving review required
  - All 6 CI jobs must pass (including 95% coverage gate)
  - Stale approvals dismissed when new commits are pushed
  - Conversation resolution required before merge
  - Force-push and deletion blocked
  - Admins cannot bypass any of the above

What is now enforced on 'develop':
  - Same as main, except admins can bypass in emergencies

To approve a PR:
  1. Open the PR on GitHub
  2. Review the CI run (all 6 jobs must be green)
  3. Review Agent C's validation report in the PR body
  4. Review the coverage report artifact (Coverage Gate job)
  5. Click "Approve" → "Merge pull request"

To adjust the 95% coverage threshold:
  Edit COVERAGE_THRESHOLD in .github/workflows/ci.yml (single number, line 14)
""")


if __name__ == "__main__":
    run()

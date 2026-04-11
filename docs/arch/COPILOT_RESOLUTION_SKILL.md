# Copilot Review Comment Resolution — Skill Reference

This document defines the workflow Claude Code must follow when resolving GitHub Copilot review comments on pull requests in this repository.

---

## Why this exists

In prior maintenance passes, Copilot comments were sometimes marked "Addressed in latest commit" without verifying that:

1. The underlying problem was actually fixed
2. The fix was applied consistently across all affected surfaces (e.g. a wrong index predicate appeared in 5 files but was only fixed in 1)
3. The comment was still relevant at HEAD (Copilot comments reference a specific commit — the code may have changed since)

This skill prevents those failure modes.

---

## Resolution workflow

### Step 1 — Read the comment in full

Before applying any fix, read:

- The **exact file and line** the comment references
- The **full comment body**, including any `suggestion` block
- The **current state of the file at HEAD** — do not assume the comment's line numbers still match

If the file or line no longer exists at HEAD, the comment may already be addressed by a prior change. Verify before skipping.

### Step 2 — Understand the problem, not just the suggestion

Copilot suggestions are sometimes:

- **Correct and complete** — apply directly
- **Correct in diagnosis, wrong in fix** — the problem is real but the suggested code is wrong or incomplete
- **Correct locally, incomplete globally** — the fix is right for this file but the same problem exists in other files

Always determine which case applies before acting.

### Step 3 — Search for the same problem elsewhere

If the comment identifies a pattern bug (e.g. wrong enum value, incorrect index predicate, inconsistent API path), search the entire PR diff for other instances of the same problem:

```bash
git diff origin/develop..HEAD -- . | grep -n "<pattern>"
```

Or use repository-wide search:

```bash
grep -rn "<pattern>" --include="*.md" --include="*.cs" --include="*.ts"
```

Fix **all instances**, not just the one the comment points to.

### Step 4 — Check against locked decisions

Before applying a fix, verify it does not conflict with:

- `CLAUDE.md` — implementation authority
- `mvp_locked_decisions.md` — locked decisions authority
- CIVIS constants and thresholds
- Schema naming conventions
- Auth strategy decisions

If a Copilot comment recommends a change that violates a locked decision, **skip the fix** and reply:

> "Skipped — conflicts with locked architecture decision. Needs manual review."

### Step 5 — Apply the fix

- Edit the file(s) at the **current HEAD line numbers**, not the line numbers from the comment
- If applying a Copilot `suggestion` block, verify it compiles/parses correctly in context before committing
- Stage only the files you changed

### Step 6 — Verify at HEAD

After committing, confirm:

- The problem described in the comment no longer exists at HEAD
- No new problems were introduced by the fix
- If the comment referenced a cross-file inconsistency, all files are now consistent

### Step 7 — Reply to the comment

Reply to each resolved comment thread:

```bash
gh api repos/{owner}/{repo}/pulls/{pr}/comments/{id}/replies \
  -X POST -f body="Addressed in latest commit."
```

If the comment was skipped (locked decision conflict, already fixed, or not applicable):

```bash
gh api repos/{owner}/{repo}/pulls/{pr}/comments/{id}/replies \
  -X POST -f body="<reason for skipping>"
```

### Step 8 — Resolve the conversation thread via GraphQL

Posting a reply does not mark the thread resolved — GitHub's branch ruleset
on `develop` requires `required_review_thread_resolution`, so unresolved
threads block merge even after all replies are posted. Always run the
GraphQL `resolveReviewThread` mutation after replying.

Get all review thread IDs for the PR. Use GraphQL variables to avoid
hardcoding the repository coordinates — replace `{owner}`, `{repo}`, and
`{pr_number}` with real values before running:

```bash
gh api graphql \
  -F owner='{owner}' \
  -F repo='{repo}' \
  -F pr={pr_number} \
  -f query='
query($owner: String!, $repo: String!, $pr: Int!) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $pr) {
      reviewThreads(first: 100) {
        nodes {
          id
          isResolved
          comments(first: 1) {
            nodes { databaseId }
          }
        }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}'
```

`databaseId` on each thread's first comment corresponds to the comment ID
used in the Step 7 REST call (`pulls/{pr}/comments/{id}`). Match on this
value to confirm you are resolving only the threads you replied to.

If `pageInfo.hasNextPage` is `true`, re-run the query passing
`after: "END_CURSOR"` to `reviewThreads` until all threads have been
retrieved.

For each unresolved thread you addressed, resolve it using its node ID:

```bash
gh api graphql -f query='
mutation {
  resolveReviewThread(input: {threadId: "THREAD_NODE_ID"}) {
    thread { isResolved }
  }
}'
```

Replace `THREAD_NODE_ID` with the node `id` from the query above.

Confirm `isResolved: true` is returned for each thread before proceeding
to merge.

---

## Handling "Outdated" comments

GitHub marks review comments as "Outdated" when the referenced lines have changed. Before skipping an outdated comment:

1. Read the comment body to understand the **underlying problem**
2. Check if the problem still exists at HEAD (it may have moved to a different line)
3. If the problem persists, fix it and reply normally
4. If the problem is genuinely resolved, reply: "Already addressed — verified at HEAD."

---

## Commit conventions

One commit per PR for all Copilot fixes (unless fixes span unrelated concerns):

```
fix: address Copilot review comments in <file or area> (<brief description>)
```

If fixes span multiple files with a common theme:

```
fix: address N Copilot review comments across <area> (<theme>)
```

---

## Post-resolution summary

After all comments on a PR are processed, post a summary comment:

```markdown
**Maintenance pass complete**

- Copilot comments addressed: <N>
- Copilot comments skipped: <N> (with reasons)
- Merge conflicts resolved: <yes/no>
- Files changed: <list>

All changes committed and pushed. Ready for re-review.
```

---

## Lesson Recording — deferred to explicit documentation passes

Do NOT auto-write to `docs/arch/LESSONS_LEARNED.md` or `docs/arch/CODING_STANDARDS.md`
during review resolution or at session end. Documentation changes happen only in a
separate, explicitly requested documentation pass.

See `docs/arch/CODING_STANDARDS.md` → "PR Scope Discipline" for the authoritative rule.

### Lesson entry template (for use during documentation passes)

```markdown
## PR #N — [PR title]

### Lesson [sequential number]: [one-line description]
**File:** `path/to/file` (or "PR-level" if not file-specific)
**What Copilot flagged:** [direct quote or close paraphrase]
**Root cause:** [why did Claude Code generate this — be specific]
**Fix applied:** [what changed]
**Rule added:** [text of new rule or existing rule name this reinforces]
```

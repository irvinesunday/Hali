# Session Universal Rules
# These rules apply to EVERY Claude Code session. Read before starting each phase.
# Version: 1.0

---

## Checkpoint commit rule (Item 20)
After completing each named phase or endpoint within this session, run:

```bash
git add -A
git commit -m "checkpoint: [brief description of what was just completed]"
```

Do this **before** starting the next phase — even within the same session.
This gives recovery points if the context window gets tight.

Example checkpoint messages:
- `checkpoint: signal_events EF Core migration applied`
- `checkpoint: POST /v1/auth/otp endpoint returns 202`
- `checkpoint: CIVIS MACF formula unit tests pass`

Do NOT wait until the entire session is complete to commit.

---

## Amnesia recovery rule (Item 23)
At the start of this session, before writing any code:

1. Run `git log --oneline -5` and read the last 5 commits
2. State in one paragraph: what was built in the prior session and what you are about to build
3. If the last commit does not match what the session prompt says should be done, **stop and report the discrepancy** — do not proceed

If you are resuming a partial session (the session was interrupted):

1. Run `git log --oneline -10`
2. Identify what was committed (what's done) and what's not yet committed (what to continue)
3. State explicitly: "Continuing from: [last checkpoint commit message]"

---

## Prompt versioning rule (Item 22)
Every session prompt has a `## Version` header at the top.
If you notice the version has changed since the last session (check git blame on the session file),
read the diff and adjust your approach accordingly before writing code.

---

## Context window rule (Item 6)
If at any point you feel like you are losing track of earlier decisions in this session:
- Stop
- Summarise what has been built so far in a brief list
- Continue from that summary

Do not let context pressure cause you to contradict earlier decisions silently.

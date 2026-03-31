## Summary
<!-- One paragraph: what does this PR do and why? -->

## Session / Phase
<!-- Which build session does this PR belong to? (e.g. Session 04 — Clustering + CIVIS) -->
Session: 
Phase: 

## Agent C Verdict
<!-- Paste the "Overall verdict" line from agent_outputs/session_N/agent_c.md -->
Verdict: 
Approved to merge: 

## Coverage
<!-- What does the Coverage Gate job report? -->
Line coverage: %
Gate status: 

## Changes made
<!-- List the key files changed and why -->
-
-

## How to test
<!-- Steps a reviewer can follow to verify this works -->
1.
2.

## Checklist
- [ ] All 6 CI jobs are green
- [ ] Coverage gate ≥ 95% 
- [ ] Agent C validation report reviewed
- [ ] No hardcoded secrets or API keys
- [ ] No `.env` files committed
- [ ] EF Core migrations are reversible
- [ ] Outbox events written in same transaction as state changes
- [ ] No features outside MVP scope introduced

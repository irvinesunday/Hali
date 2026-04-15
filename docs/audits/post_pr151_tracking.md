# Post-PR-151 Refactor Path — Tracking

Companion to `docs/audits/refactor_path_post_pr151_audit.md`.
Lists the GitHub issues created to track the remaining refactor/cleanup work,
in recommended execution order.

## Execution order

| # | Issue | Title | Classification |
|---|---|---|---|
| 1 | #152 | Mobile error parser discards canonical error envelope | Adjacent debt — blocks the practical value of H2; must land before H3 renames |
| 2 | #153 | H3: Central reason-code catalog + consistency pass | Original refactor-path work (H3) |
| 3 | #154 | Mobile ErrorCode discriminated union | Original refactor-path follow-on to H3 (mobile side) |
| 4 | #155 | LocalitiesController catch-all must exclude OperationCanceledException | Adjacent debt — standing-rule violation |
| 5 | #156 | Implement POST /v1/feedback persistence | Separate technical debt — not part of the original refactor path |

Land order 1 → 2 → 3 is a chain (each depends on the previous). Items 4 and 5
are independent and can merge in parallel.

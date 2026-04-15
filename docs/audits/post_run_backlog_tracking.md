# Post-Run Backlog Tracking

Backlog issues created from `docs/audits/post_run_rebaseline_audit.md`.

Recommended execution order (top → bottom):

| Order | Audit item | Issue | Title |
|---|---|---|---|
| 1 | R2 | [#164](https://github.com/irvinesunday/Hali/issues/164) | Error-code metrics in ExceptionHandlingMiddleware |
| 2 | R1 | [#165](https://github.com/irvinesunday/Hali/issues/165) | Nullable-safety cleanup tranche (drive CS8600 warnings to zero) |
| 3 | R3.a | [#166](https://github.com/irvinesunday/Hali/issues/166) | Home feed observability metrics |
| 4 | R3.b | [#167](https://github.com/irvinesunday/Hali/issues/167) | Signals observability metrics (ingestion throughput, NLP latency, join-rate) |
| 5 | R3.c | [#168](https://github.com/irvinesunday/Hali/issues/168) | Participation and clusters observability metrics |
| 6 | R4 | [#169](https://github.com/irvinesunday/Hali/issues/169) | Feedback endpoint rate limiting + 429/OpenAPI restoration |
| 7 | R3.d | [#170](https://github.com/irvinesunday/Hali/issues/170) | Notifications observability metrics (push send success/failure + latency) |

#164 and #165 have no dependency on each other and can proceed in parallel. #166–#168 and #170 should follow the meter-registration pattern established by #164. #169 is independent of the observability arc but is the audit's recommended product-hardening follow-up now that feedback persistence has landed.

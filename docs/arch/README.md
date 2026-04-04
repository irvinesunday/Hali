# Hali Architecture Documents

Read these files in Claude Code before implementing each area.

| File | Purpose | When to read |
|---|---|---|
| `01_schema_reference.md` | Canonical DDL, all tables, constraints, indexes | Before any DB migration |
| `02_api_contracts.md` | All routes, request/response shapes | Before any endpoint work |
| `03_phase1_backend.md` | Backend build order, gates, C# patterns | Phase 1 backend work |
| `04_phase1_mobile.md` | Mobile screen inventory, navigation, state | Phase 1 mobile work |
| `05_civis_engine.md` | WRAB, SDS, MACF, lifecycle, restoration math | Any CIVIS/clustering work |
| `06_worker_pipelines.md` | All background workers, queues, retry policy | Any worker implementation |
| `07_auth_implementation.md` | Auth flows for all surfaces, token strategy | Auth for any surface |
| `08_phase2_institution.md` | Institution dashboard surfaces | Phase 2 work |
| `09_nlp_integration.md` | Anthropic API integration, validation rules | CSI-NLP work |
| `10_testing_strategy.md` | Unit, integration, contract test requirements | Any new module |

## Root file

`CLAUDE.md` at repo root is the master engineering brief. Claude Code reads this automatically.
Always read it first.

## Reconciliation document

`Hali_Platform_Reconciliation_v1.md` in repo root documents all conflicts resolved and gaps filled
across the original build pack and dashboard architecture pack. If you find a conflict between
any doc and the reconciliation document, the reconciliation document wins.

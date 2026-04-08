# Doc Audit Report — 2026-04-08

Audit of every `.md` instruction/reference doc under `docs/` and at the repo
root, classified by whether `CLAUDE.md` references its filename.

## Wired (already referenced in CLAUDE.md before this audit)

- `docs/arch/00_session_patch_notes.md`
- `docs/arch/01_schema_reference.md`
- `docs/arch/02_api_contracts.md`
- `docs/arch/03_phase1_backend.md`
- `docs/arch/04_phase1_mobile.md`
- `docs/arch/05_civis_engine.md`
- `docs/arch/06_worker_pipelines.md`
- `docs/arch/07_auth_implementation.md`
- `docs/arch/08_phase2_institution.md`
- `docs/arch/09_nlp_integration.md`
- `docs/arch/10_testing_strategy.md`
- `docs/arch/SELF_HEALING_SKILL.md`
- `Hali_Platform_Reconciliation_v1.md`

## Newly wired in this commit

### ACTIVE_SKILL — added under "Reference Skills"

- `mvp_locked_decisions.md` — locked-decisions authority referenced by `COPILOT_RESOLUTION_SKILL.md`. Must be consulted before changing any locked decision.
- `nlp_extraction_prompt.md` — defines the bounded structured-extraction contract for CSI-NLP. Must be read before touching extraction prompts/schemas.
- `mobile_screen_inventory.md` — locks the Phase 1 mobile screen set. Must be read before adding/removing/restructuring citizen-mobile screens.

### REFERENCE_DOC — added under "Architecture Reference Docs"

- `docs/arch/README.md` — index of the docs/arch/ canon
- `docs/arch/hali_citizen_mvp_canonical_spec.md` — Phase 1 citizen UX/architecture freeze
- `docs/arch/hali_institution_dashboard_canonical_spec.md` — Phase 2 institution dashboard freeze
- `docs/arch/hali_ops_internal_dashboard_canonical_spec_v2.md` — Phase 3 ops dashboard freeze
- `docs/runbooks/*.md` (13 files) — incident response runbooks
- `docs/staging-env-guide.md` — staging GitHub Environment secret reference
- `04_queue_topic_definitions.md` — domain event / queue topic catalog
- `05_redis_job_config.md` — Redis usage and worker queue config
- `schema_patch_notes.md` — historical schema patch notes
- `HANDOVER.md` — operator guide for Claude Code build sessions
- `SECURITY.md` — vulnerability reporting policy

## Pending in another PR

- `docs/arch/COPILOT_RESOLUTION_SKILL.md` — wiring is the subject of PR #70, intentionally not duplicated here to avoid merge conflicts.

## Stale / superseded — NOT wired (reasons noted)

- `CONSOLIDATION_GUIDE.md` — one-time merge guide for the original "Architecture Pack + Agent Framework" zip consolidation; no longer relevant after the repo was assembled.
- `openapi_patch_checklist.md` — one-time patch task that was completed during the original spec freeze; the canonical OpenAPI is now `02_openapi.yaml`.
- `sync_prompt.md` — one-time synchronisation prompt for an earlier hand-off; superseded by CLAUDE.md itself as the master brief.
- `docs/arch/pr50-followup-gaps.md` — gap notes scoped to a single old PR (`fix/mobile-pr50-followups`); historical only, not actionable instructions.
- `docs/arch/pr56-enum-fix-notes.md` — post-mortem notes for PR #56's Npgsql enum fix; historical only.

## Method

```bash
while IFS= read -r f; do
  n=$(basename "$f")
  if grep -q "$n" CLAUDE.md; then echo "WIRED: $f"; else echo "ORPHAN: $f"; fi
done < <(find docs/ -name "*.md" | sort)
```

(Same loop run for top-level `.md` files filtered against README/CHANGELOG/LICENSE/CONTRIBUTING/CLAUDE.)

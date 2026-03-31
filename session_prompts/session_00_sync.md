## Version: 1.0
## Last updated: 2026-03-30

# Session 00 — Synchronisation Patch
# Run this FIRST, before any code is written.

Read claude.md fully before doing anything else.

Then read and execute sync_prompt.md exactly as written.

Your four tasks are:
1. Patch 02_openapi.yaml per openapi_patch_checklist.md
2. Scaffold EF Core migration files per schema_patch_notes.md
3. Run seed_taxonomy_expanded.sql (covers both taxonomy tables)
4. Align frontend API call stubs to the patched OpenAPI spec

Show a change plan for each task before implementing it.
Do not proceed to Phase 1 of the main build in this session.
When all four tasks are done, output: SESSION_00_COMPLETE

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---

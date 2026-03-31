## Version: 1.0
## Last updated: 2026-03-30

# Session 01 — Foundation (Phases 1 + 2)
# Prerequisite: Session 00 complete and committed.

## Context
Session 00 is complete. The OpenAPI spec is patched, EF Core migration files are
scaffolded, and the taxonomy is seeded. The repo has a clean commit at this baseline.

## Your task this session
Build Phases 1 and 2 only. Do not proceed to Phase 3.

### Phase 1 — Repo structure
Scaffold the full solution per Folder_Structure.md:
- Hali.Api, Hali.Application, Hali.Domain, Hali.Infrastructure, Hali.Workers, Hali.Contracts
- Test projects: unit, integration
- Scripts folder with seed_taxonomy_expanded.sql reference
- .gitignore, README.md, docker-compose.yml (already present — do not overwrite)

Use the Architecture section of claude.md as the definitive layout.
Do NOT follow 10_repository_structure.md — it is superseded.

### Phase 2 — Core schema migrations
Generate or verify EF Core code-first migration files per schema_patch_notes.md.
Module-to-table mapping is in sync_prompt.md Task 2.
Every index in 01_postgres_schema.sql must be present in the migrations.

Show the full structure before writing any code.
Confirm each project scaffolds and the solution builds (`dotnet build`) before proceeding.

## Done when
- `dotnet build` passes with zero errors
- All 6 module migration sets exist with correct tables
- Output: SESSION_01_COMPLETE

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---

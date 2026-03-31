# Hali MVP — Synchronisation Prompt for Claude Code

Read and follow the updated docs in this pack as the new implementation authority.
Your task is not to redesign anything. Your task is to synchronise the existing
repo artifacts to these locked decisions.

Before starting, read these files in order:
1. claude.md
2. mvp_locked_decisions.md
3. openapi_patch_checklist.md
4. schema_patch_notes.md
5. 01_postgres_schema.sql

---

## Task 1 — Patch the OpenAPI spec (02_openapi.yaml)

Follow openapi_patch_checklist.md exactly. It has four steps:

**Step 1 — Remove old paths.** Delete these paths from 02_openapi.yaml entirely:
- POST /v1/auth/request-otp
- POST /v1/auth/verify-otp
- POST /v1/clusters/{clusterId}/participations
- POST /v1/signals
- POST /v1/localities/followed (replace with PUT)

**Step 2 — Add all required paths** listed in the checklist.

**Step 3 — Update component schemas:**
- Add conditionConfidence, locationSource, locationPrecisionType to SignalCandidate
- Add HomeFeedResponse and HomeFeedClusterItem as new inline schemas

**Step 4 — Wire GET /v1/home** to return HomeFeedResponse.

Show the complete diff before applying. Do not leave any old paths alongside new ones.

---

## Task 2 — Generate EF Core migration files

The schema DDL in 01_postgres_schema.sql is already fully patched and is the
reference. Do not re-patch the DDL file.

Generate EF Core code-first migration files, one migration set per module,
covering every table in the DDL. Follow schema_patch_notes.md for the full list
of changes these migrations must implement.

Module → tables mapping:
- Auth: accounts, devices, otp_challenges, refresh_tokens
- Signals: localities, location_labels, taxonomy_categories, taxonomy_conditions, signal_events
- Clusters: signal_clusters, cluster_event_links, civis_decisions, outbox_events
- Participation: participations
- Advisories: official_posts, official_post_scopes, institutions, institution_jurisdictions
- Notifications: notifications, follows
- Admin: admin_audit_logs

Each migration must include all indexes defined in 01_postgres_schema.sql for that
module's tables, including GiST indexes (see schema_patch_notes.md §12).

Show the migration plan (file names, table list per migration) before generating code.

---

## Task 3 — Seed the database

Run `seed_taxonomy_expanded.sql` as the single canonical taxonomy seed.
This file covers both `taxonomy_categories` (28 subcategories) and
`taxonomy_conditions` (all 8 categories) with ON CONFLICT guards.

Do NOT run `03_seed_taxonomy.sql` — it is deprecated and superseded.
`seed_taxonomy_expanded.sql` is safe to re-run at any time.

---

## Task 4 — Align frontend API usage to the patched OpenAPI spec

Update all API call sites in the React Native app to use the paths in
mobile_screen_inventory.md (which already matches the patched spec).

Key changes:
- POST /v1/auth/otp (was /request-otp)
- POST /v1/auth/verify (was /verify-otp)
- POST /v1/signals/submit (was /v1/signals)
- POST /v1/clusters/{id}/participation (was /participations, plural)
- PUT /v1/localities/followed (was POST)
- Add POST /v1/auth/refresh call on app resume / 401 response

---

## Rules

- Show a concise change plan before implementing each task
- Do not introduce new features or change locked product decisions
- Do not modify claude.md, mvp_locked_decisions.md, or any spec file
- When uncertain: stop and ask

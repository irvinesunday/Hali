# PR #50 Follow-up Gaps

Gaps discovered while working `fix/mobile-pr50-followups` (branched off
`develop@0aabb71`). Items here were deferred because completing them requires
work outside the scope of a mobile-only fix branch.

---

## Gap 1 — Canonical locality names cannot be rendered in the citizen app

**Status:** Task 1 of the PR #50 follow-ups deferred in full.
**Symptom:** `apps/citizen-mobile/app/(app)/settings/wards.tsx` renders
followed wards as raw UUID strings (e.g. `a3f9c012-…`) and the "add ward"
flow is a UUID `TextInput`, not a name picker.

### Why it cannot be fixed in the mobile app alone

The data exists in the database but is not exposed by any API:

| Layer | State |
|---|---|
| `Locality` entity | Has `WardName`, `CityName`, `CountyName`, `WardCode` columns (`src/Hali.Infrastructure/Data/Signals/SignalsDbContext.cs:37-43`). |
| `IFollowService.GetFollowedAsync` | Returns `Follow` entities (ID pairs only). No locality projection. |
| `GET /v1/localities/followed` | Returns `{ localityIds: string[] }` only (`src/Hali.Api/Controllers/LocalitiesController.cs:34`). |
| `GET /v1/localities/{id}` | Does not exist. |
| `GET /v1/localities?search=` | Does not exist. |
| `ClusterResponseDto` (home feed) | Does not include locality name (`src/Hali.Api/Controllers/HomeController.cs:230`). No client-side back-fill is possible. |
| `FollowedLocalitiesResponse` (mobile type) | `{ localityIds: string[] }` — mirrors the wire shape (`apps/citizen-mobile/src/types/api.ts:80`). |
| `LocalityContext` | Tracks IDs only. |

There is no API path the mobile app can call to learn the name "South B"
for any locality ID, so no purely-mobile change can render canonical names.

### Required follow-up work (separate PR)

Minimum scope to unblock the mobile fix:

1. **Backend — extend follow read path**
   - Update `IFollowService.GetFollowedAsync` (or add a sibling method) to
     return locality projections, not bare `Follow` entities. Join the
     `Locality` table.
   - Update `LocalitiesController.GetFollowed` to return
     `[{ id, wardName, cityName, countyName, wardCode }, ...]`.
     Decide whether to keep `localityIds` for backwards compatibility or
     break the shape (this branch would have broken it; safer to add a
     parallel field).
   - Update OpenAPI spec for `GET /v1/localities/followed`.
   - Unit test the join.

2. **Backend — add a name lookup or search endpoint**
   Needed so the "add ward" UI can show names instead of asking users to
   paste a UUID. Two options:
   - `GET /v1/localities/{id}` — single lookup. Simpler, but the picker
     still needs *some* way to find IDs. Only useful if combined with a
     known locality list source.
   - `GET /v1/localities?search=<q>&limit=20` — name search. Required for
     a real picker. Needs PostgreSQL trigram or `LIKE` index on `ward_name`.
     Define rate-limit policy and auth requirement.

3. **Mobile — render and pick by name**
   - Update `FollowedLocalitiesResponse` and `LocalityContext` to carry
     `{ id, wardName, cityName, countyName }` for each followed entry.
     Keep ID as the stable key passed to mutations.
   - Replace the UUID `TextInput` in `wards.tsx` with a search-driven
     picker against the new search endpoint.
   - Update the home header pill (`home.tsx`, the only other consumer of
     `activeLocalityId`) to show the active ward's `wardName`.
   - Audit any cluster detail / composer surfaces for other UUID leakage
     once the data plumbing is in place.

### Acceptance criteria for the follow-up PR

- Ward picker shows e.g. "South B" in `wards.tsx` instead of any UUID
  fragment.
- Home header pill shows the active ward's canonical name.
- Adding a ward is a name search, not a UUID paste.
- `GET /v1/localities/followed` documented in OpenAPI to include the new
  fields.
- No raw `account_id` or CIVIS internals introduced by the new responses.

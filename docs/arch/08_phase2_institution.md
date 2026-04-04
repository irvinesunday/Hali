# Hali — Phase 2 Institution Surfaces Implementation Guide
**Institution Operations Dashboard + Institution Admin Dashboard.**

Do not start Phase 2 until Phase 1 has passed all integration tests and is in pilot operation.

---

## Pre-work before any Phase 2 code

- [ ] Run Phase 2 schema migrations: `institution_memberships`, `institution_user_scopes`, `official_update_templates`, `institution_notification_recipients`
- [ ] Extend `admin_audit_logs` with new columns
- [ ] Implement magic link + TOTP auth flow (see `docs/arch/07_auth_implementation.md`)
- [ ] Set up monorepo `apps/institution-web` and `apps/institution-admin-web` with Next.js
- [ ] Configure pnpm workspace + Turborepo task pipeline
- [ ] Create `packages/design-system` with shared Tailwind tokens and shadcn/ui primitives

---

## Institution Operations Dashboard

### Stack

```
apps/institution-web/
  app/
    (auth)/
      login/page.tsx       -- email entry + magic link
      verify-totp/page.tsx -- TOTP entry
    (dashboard)/
      layout.tsx           -- sidebar nav + auth guard
      page.tsx             -- overview (default)
      live-signals/
        page.tsx           -- signal cluster table
        [clusterId]/
          page.tsx         -- cluster detail (institution view)
      official-updates/
        page.tsx           -- updates list
        new/page.tsx       -- create update
        [id]/
          page.tsx         -- update detail + edit
      scheduled-disruptions/
        page.tsx
      restoration/
        page.tsx           -- restoration queue
      coverage-areas/
        page.tsx
      activity/
        page.tsx           -- activity log
  components/
  lib/
    api.ts                 -- typed API client
    auth.ts                -- cookie-based auth
```

---

### Polling strategy

All dashboard pages poll at the following intervals when the browser tab is active:

```typescript
// lib/polling.ts
const POLL_INTERVALS = {
    overview: 60_000,      // 60 seconds
    liveSignals: 30_000,   // 30 seconds
    restoration: 30_000,   // 30 seconds
    activityLog: 60_000,
} as const;

// Pause when tab is backgrounded
document.addEventListener('visibilitychange', () => {
    if (document.hidden) pauseAllPolling();
    else { resumeAllPolling(); refetchAll(); }
});
```

Show `freshnessTimestamp` in the overview header: "Last updated 42 seconds ago".

---

### Data visibility rules (institution-specific)

The institution-facing cluster response **must not** include:

```typescript
// These fields must be absent from InstitutionClusterSummaryDto and InstitutionClusterDetailDto:
// civis_score, wrab, sds, macf, raw_confirmation_count
// account_id, device_id on any sub-object
// civis_precheck, device_integrity_level

// What institutions CAN see:
type InstitutionClusterSummaryDto = {
    id: string;
    category: CivicCategory;
    subcategorySlug: string;
    dominantConditionSlug: string;
    state: ClusterState;
    temporalType: string;
    title: string;
    summary: string;
    locationLabel: string;
    publicConfirmationCount: number;
    affectedCount: number;
    observingCount: number;
    firstSeenAt: string;
    lastSeenAt: string;
    restorationState: 'none' | 'possible' | 'confirmed';
    linkedOfficialUpdatesCount: number;  // how many of THEIR updates reference this cluster
};
```

---

### Official update creation form

```
Form fields:
  - Post type (Live Update / Scheduled Disruption / Advisory/Public Notice) — required
  - Category — required
  - Title — required, max 220 chars
  - Body — required, max 5000 chars, rich text editor acceptable
  - Start date/time — required for Scheduled Disruption
  - End date/time — required for Scheduled Disruption
  - Related cluster — optional search/select from clusters in scope
  - Is restoration claim — checkbox, only shown when related cluster is set
    - When checked: show warning "This will trigger restoration verification for citizens"
  - Scope — pre-filled with user's geo scope, editable within that scope
  - Template selector — loads from /v1/institution-admin/templates

Validation:
  - Scheduled disruptions must have starts_at < ends_at
  - Restoration claim requires related_cluster_id
  - Scopes must be within institution's assigned jurisdiction (server validates — show error if rejected)
```

---

### Editing rules (enforce in UI)

| Post type | Editable | Conditions |
|---|---|---|
| live_update | body only | while status = published |
| scheduled_disruption | title, body, starts_at, ends_at, scopes | while draft, or published if starts_at is future |
| advisory_public_notice | body only | while status = published |

Withdrawn posts: read-only. Show "Withdrawn" badge. Withdrawal available to manager/admin role.

`is_restoration_claim = true` posts: immutable after publish. No edit path.

---

## Institution Admin Dashboard

### Stack

```
apps/institution-admin-web/
  app/
    (auth)/           -- same magic link + TOTP flow
    (admin)/
      layout.tsx
      users/
        page.tsx      -- user list
        invite/page.tsx
        [id]/page.tsx -- user detail + role/scope management
      roles/
        page.tsx      -- role matrix viewer
      scopes/
        page.tsx      -- geo scope management
      category-scopes/
        page.tsx
      templates/
        page.tsx
        new/page.tsx
        [id]/page.tsx
      notification-recipients/
        page.tsx
      audit/
        page.tsx
      settings/
        page.tsx
```

---

### User invitation flow

```
1. Institution Admin opens /users/invite
2. Fills: email address, role (viewer/operator/manager/admin)
3. Optionally assigns geo scopes (subset of institution's jurisdictions)
4. POST /v1/institution-admin/users
   Server:
   - Creates accounts row if email not found (account_type: institution_user)
   - Creates institution_memberships row
   - Sends magic link email to new user
   - New user's first login triggers TOTP enrollment
5. User appears in /users list with status: "Pending first login"
```

---

### Role permission display

Show a read-only matrix on `/roles` page:

| Permission | Viewer | Operator | Manager | Admin |
|---|---|---|---|---|
| View clusters in scope | ✓ | ✓ | ✓ | ✓ |
| Post official updates | | ✓ | ✓ | ✓ |
| Withdraw own updates | | ✓ | ✓ | ✓ |
| Withdraw team updates | | | ✓ | ✓ |
| View activity log | | | ✓ | ✓ |
| Manage users | | | | ✓ |
| Manage scopes | | | | ✓ |
| Manage templates | | ✓ | ✓ | ✓ |

---

## Shared backend changes for Phase 2

### New authorization policies

```csharp
// Register in Phase 2:
options.AddPolicy("InstitutionOperator", policy =>
    policy.RequireInstitutionRole("institution_operator", "institution_manager", "institution_admin"));

options.AddPolicy("InstitutionAdmin", policy =>
    policy.RequireInstitutionRole("institution_admin"));

// Scope enforcement middleware — runs after role check:
// For every /v1/institution/* request:
// 1. Resolve acting user's institution_id from InstitutionMemberships
// 2. Resolve their geo scopes from institution_user_scopes (or fallback to institution_jurisdictions)
// 3. Inject scope context into the request
// 4. All queries in institution-facing services apply scope filter automatically
```

### Institution cluster query

```sql
-- All institution cluster queries must include scope filter:
SELECT sc.*
FROM signal_clusters sc
WHERE sc.state IN ('active', 'possible_restoration', 'unconfirmed')
  AND (
    sc.locality_id IN (
        SELECT ij.locality_id
        FROM institution_jurisdictions ij
        WHERE ij.institution_id = @institutionId
    )
    OR ST_Intersects(sc.centroid, (
        SELECT ST_Union(ij.geom)
        FROM institution_jurisdictions ij
        WHERE ij.institution_id = @institutionId
    ))
  )
ORDER BY sc.last_seen_at DESC
```

### Institution notification pipeline

When a cluster transitions to `active`, the notification worker must:

```
1. Look up institution_notification_recipients
   WHERE locality match (locality_id in institution_jurisdictions)
   AND category match (or institution has no category restriction)
   AND notification_type = 'cluster_activated_in_scope'
   AND is_active = true
2. For each recipient: create notifications row (channel: email)
3. Email body: cluster summary, location, category, link to institution ops dashboard
4. NotificationWorker sends via email provider
```

---

## Phase 2 integration tests

```
✓ Magic link issued → TOTP enrolled → session established
✓ Institution cluster query returns only clusters in jurisdiction
✓ Institution cluster query never returns civis_score or raw_confirmation_count
✓ Official update posted → appears in citizen home feed for scoped locality
✓ Official update with is_restoration_claim → cluster enters possible_restoration
✓ Institution user with operator role cannot access institution-admin routes
✓ Institution user cannot see other institutions' data (cross-institution leak test)
✓ Geo scope enforcement: user with ward-restricted scope cannot see out-of-scope clusters
✓ Institution notification recipient receives email when cluster activates in scope
✓ User invitation: email sent, account created, TOTP enrollment on first login
```

# Hali Feature Flighting Model

Canonical policy for feature flags on Hali. This document defines **what**
gets flagged, **how** flags are named, scoped, rolled out, and retired,
and the distinction between server-only and client-visible flags.

Feature flighting on Hali is intentionally **minimal and typed**. We do
not run a flag platform. We run a narrow, typed flag registry serving a
handful of deliberate use cases. Flag sprawl is more expensive than the
flexibility it buys, so the default answer to "should this be a flag?" is
no.

Authority hierarchy: this document sits below `CLAUDE.md`,
`Hali_Platform_Reconciliation_v1.md`, `docs/arch/SECURITY_POSTURE.md`, and
`docs/arch/OBSERVABILITY_MODEL.md`, and above the flag-registry
implementation code.

The concrete backend implementation lives in the backend flag-registry
module (under `src/Hali.Application` once introduced). The client-safe
flag exposure contract lives in the client-facing flag contract.
Citizen-mobile consumption and institution-web day-one integration live
in their respective application codebases. Tracking links to the
in-flight issues belong in those PR descriptions, not in this document.

---

## 1. Principles

1. **Minimal surface.** If a flag can be avoided by shipping behind an API
   feature freeze or by unshipping a nearly-ready change, do that instead.
2. **Typed, not stringly.** Every flag is defined as a typed member of the
   registry. No `GetFlag("enable_thing")` calls with arbitrary string keys
   — that is flag sprawl dressed up as flexibility.
3. **Server-decided.** Flag evaluation happens server-side. Clients
   receive the *resolved* value for the current request context, not the
   rules that produced it. Clients never see rules that could leak
   rollout strategy or targeted-audience information.
4. **Every flag expires.** A flag's entry in the registry includes an
   owner and an expected-retirement date. Long-lived feature toggles are a
   code smell; ops kill switches are the one exception.
5. **Frontend flag hiding is not security.** Flags hide work in progress.
   They do not enforce authorization. If a surface must be gated, gate it
   server-side in addition to hiding it client-side.
6. **One write site.** Changes to a flag's default or scope go through the
   registry definition file. Never through environment overrides
   exclusively — a prod-only override with no code trace is an outage
   waiting to happen.

---

## 2. What gets flagged

### 2.1 In scope

- **Dark-launch rollouts** — a new endpoint, screen, or worker job that
  is deployable but should not yet take traffic.
- **Pilot-audience rollouts** — scoping a feature to a specific
  institution or specific set of localities before a full rollout.
- **Emergency kill switches** — ability to disable a non-core code path
  quickly via the typed flag registry and a fast deploy, without a new
  code change at the call site. Kill switches are a deliberate permanent
  category; they do not expire.
- **Operator-only / internal-only surfaces** — experimental internal
  screens or ops tooling that should not appear to external users, ever.
- **Controlled degradation** — e.g. turn off a cache refresh worker if
  it is causing load; clients see graceful degradation.

### 2.2 Out of scope

- **A/B or multivariate experimentation.** Hali is civic infrastructure,
  not a growth product. If we need experimentation later, it gets its own
  system — not this registry.
- **User-level personalisation.** Flags target the platform, an
  institution, or a locality — not an individual citizen account.
- **Authorization decisions.** Scope and role checks are not flags. They
  are policy-enforced server-side.
- **Config values.** Thresholds, timeouts, feature parameters (CIVIS
  tuning, token lifetimes, rate-limit budgets) live in typed options
  classes (`AuthOptions`, `CivisOptions`, etc.), not in the flag registry.
- **Anything in the Phase 1 out-of-scope list** (comments, messaging,
  likes, etc.). Flags do not dark-launch features that violate the
  doctrine; they dark-launch features that satisfy it.

---

## 3. Flag dimensions

Every flag is evaluated against at most four inputs. These are the only
targeting axes supported.

| Axis | Values | When to use |
|---|---|---|
| Environment | `development`, `staging`, `production` | Most flags default `on` in non-prod, off in prod during ramp |
| Institution id | specific `institution_id` (or `*` for all) | Institution-specific pilots (e.g. give Institution X access to the new update template first) |
| Locality id | specific `locality_id` (or set) | Locality-scoped rollouts (e.g. new flow enabled in Nairobi wards first) |
| Actor type | `citizen`, `institution`, `admin` (JWT role claim today; expands as future roles land, e.g. `hali_ops`) | Internal-only / admin-only features |

Combining axes (e.g. `environment == production AND institution_id IN (…)`)
is allowed. Anything more expressive than that is out of scope — if you
need it, you likely need a different abstraction.

---

## 4. Server-only vs client-visible flags

This is the single most important distinction in the model. Getting it
wrong leaks strategy and creates cross-surface bugs.

### 4.1 Server-only flags

A flag is **server-only** by default. It is used to gate server
behaviour — an endpoint handler path, a worker job registration, a
cache warming pass — and its resolved value is never returned to any
client.

Examples:

- `workers.outbox_relay.enabled` — if off, the outbox relay doesn't
  pick up new jobs.
- `workers.lifecycle_manager.enabled` — gates a planned central
  cluster-lifecycle manager migration while it is dark-launched.
- `api.nlp_fallback.enabled` — controls whether the NLP pipeline falls
  back to a stub when Anthropic is unreachable.

### 4.2 Client-visible flags

A flag must be explicitly **tagged as client-visible** in the registry
to be eligible for exposure to any client. The client-safe flag
endpoint / boot payload only returns flags with that tag. Every
other flag remains server-only regardless of how it is named.

Examples:

- `mobile.signal_composer.voice_input.enabled` — shows the voice-input
  affordance in the composer.
- `mobile.home.condition_badge.enabled` — controls whether the
  condition badge ships in the cluster header.
- `institution_web.restoration_ui.enabled` — shows the restoration
  action UI to institution users.

### 4.3 Never expose internals

Client-visible flags return **only the resolved boolean / enum value**
for the current request. They never return:

- The list of institutions targeted by a pilot flag
- The locality set that matched
- Internal flag names that should remain private
- Any rollout percentage or strategy metadata

A client that needs to know "is X on for me" gets a single boolean per
flag it consumes. If the client needs to distinguish between two states
of "on", the flag returns a typed enum — still a single resolved value.

---

## 5. Naming conventions

Flag names are dot-delimited, lowercase, `snake_case` segments, and
always shaped as:

```
<surface>.<feature>.<dimension>
```

where:

- `<surface>` is one of `api`, `workers`, `mobile`, `institution_web`,
  `institution_admin_web`, `hali_ops_web`.
- `<feature>` is the feature being gated, named positively (what turning
  the flag on does — not what it prevents).
- `<dimension>` is the unit of control. Usually `enabled` for booleans;
  a typed-enum flag may use a more specific dimension name (e.g.
  `strategy`).

Examples:

- `api.home.cursor_pagination.enabled`
- `workers.outbox_relay.enabled`
- `mobile.home.condition_badge.enabled`
- `institution_web.restoration_ui.enabled`
- `api.nlp.provider.strategy` → enum `{ anthropic, stub }`

Rules:

- **Never rename a live flag.** Add a new one, dark-launch it, flip to
  the new name across all consumers, retire the old one.
- **Never use a negative framing.** `feature.enabled` flips on to
  enable; no `feature.disabled` flags.
- **Never encode the target audience in the name.** `mobile.x.enabled`
  is one flag that can be scoped to an institution, not
  `mobile.x.acme_institution.enabled`.
- **Never reuse a retired flag name.** Append a `.v2` if the meaning
  has changed.

---

## 6. Ownership, expiry, cleanup

Every registry entry includes:

| Field | Meaning |
|---|---|
| `name` | Canonical flag name (see §5) |
| `description` | One-sentence description — what the flag controls |
| `owner` | GitHub handle or team alias responsible for retiring the flag |
| `created_at` | When the flag was added |
| `expected_retirement` | Best-effort date or "permanent (ops kill switch)" |
| `kind` | `dark_launch`, `pilot`, `kill_switch`, `internal_only` |
| `visibility` | `server_only` or `client_visible` |
| `default` | Default resolved value if no targeting matches |

Retirement discipline:

- A `dark_launch` flag retires once the feature is fully on in
  production and has been stable for one release cycle. Retirement means
  deleting the flag entry, the branch behind it, and any consumer-side
  reference.
- A `pilot` flag retires once the pilot scope has been broadened to
  match the `dark_launch` retirement criterion.
- `kill_switch` flags do not retire. They live in the registry with an
  explicit `permanent` marker.
- An expired flag that has not been retired appears as a CI warning
  emitted by the flag-registry tooling. The warning can be dismissed
  with an explicit extension on the registry entry — but doing so
  requires an updated `expected_retirement` and a short reason.

---

## 7. Rollout and rollback planning

When adding a flag, the PR description must state:

1. What the flag controls
2. Who owns it
3. The rollout plan — initial scope, expansion steps, expected
   end state
4. The rollback plan — how to turn it off if something breaks, and
   what observable signals would trigger rollback
5. The retirement plan — expected retirement date and what must be
   true before retirement

A flag without a rollout / rollback / retirement plan does not ship.

Kill switches are exempt from the rollout / retirement plan but must
state (1), (2), and a rollback plan (which for kill switches is usually
"flip it off; no rollback needed").

---

## 8. Emergency kill-switch policy

### 8.1 What qualifies

- A non-core code path that can be disabled without a redeploy and
  without breaking the core citizen experience. Examples:
  - Home feed freshness cache refresh
  - Push notification dispatch
  - NLP preview fallback
- The core citizen flows (auth, signal submit, home feed read) do
  **not** have kill switches. Breaking those is a production outage;
  the correct response is rollback, not silent degradation.

### 8.2 Flip criteria

- A kill switch is flipped by an authorized ops actor via a registry
  edit and a fast deploy (the typed registry is the single write site;
  there is no external flag UI).
- Every flip emits both a structured log event and an audit-trail
  record (per `docs/arch/SECURITY_POSTURE.md` §7 and
  `docs/arch/OBSERVABILITY_MODEL.md` §2).
- Every flip is communicated to the engineering-on-call channel within
  5 minutes.

### 8.3 Post-flip

- A kill switch flipped on (i.e. disabling the feature) creates an
  incident ticket automatically — see the runbook associated with the
  flag.
- A kill switch stays flipped until the root cause is understood and
  fixed in code. It is not a substitute for a fix.

---

## 9. Examples

### 9.1 Citizen mobile (client-visible)

```
name: mobile.signal_composer.voice_input.enabled
description: Shows the voice-input affordance in the signal composer.
owner: @irvinesunday
kind: dark_launch
visibility: client_visible
default: false
targeting:
  - if environment == development → true
  - else → false
expected_retirement: "2026-06-15 or after first pilot cohort"
```

### 9.2 Institution web (client-visible, institution-scoped)

```
name: institution_web.restoration_ui.enabled
description: Shows the institution restoration action UI.
owner: @irvinesunday
kind: pilot
visibility: client_visible
default: false
targeting:
  - if institution_id in {
      11111111-1111-1111-1111-111111111111,
      22222222-2222-2222-2222-222222222222
    } → true
  - else → false
expected_retirement: "broaden once pilot institutions approve"
```

### 9.3 Backend server-only (kill switch)

```
name: workers.push_dispatcher.enabled
description: Disables the Expo push dispatcher worker job pickup.
owner: @irvinesunday
kind: kill_switch
visibility: server_only
default: true
targeting: []
expected_retirement: permanent
```

### 9.4 Hali-ops (internal-only, future surface)

```
name: hali_ops_web.moderation_queue.enabled
description: Shows the moderation queue screen to Hali-ops actors.
owner: @irvinesunday
kind: internal_only
visibility: client_visible
default: false
targeting:
  - if actor_type == hali_ops AND environment == production → true
  - else → false
expected_retirement: "permanent — staff-only surface"
```

---

## 10. What this model does not do

- It does not hold user-level data or individual opt-ins
- It does not record exposure events for experimentation analysis
- It does not support time-windowed rollouts beyond what a scheduled
  registry edit + deploy achieves
- It does not override server-side authorization
- It does not live outside the code repository — there is no external
  flag UI to flip flags without a PR

These omissions are deliberate. If any of them becomes genuinely
necessary, address it by extending the model here first — never by
bolting on an external system.

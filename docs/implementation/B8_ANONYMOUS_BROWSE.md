# B8 Anonymous Browse

## What changed

Anonymous (unauthenticated) users can now open the app and browse the public
home feed and cluster detail screens without signing in. Contribution actions
(signal creation, participation, context, restoration) remain gated behind
authentication on both server and client.

### Backend

- **HomeController**: Removed the `isAuthenticated` guard on the `?localityId`
  query parameter. Anonymous callers can now pass `?localityId=<guid>` to scope
  the feed to a specific locality. Previously only authenticated users could
  use this parameter; anonymous callers always received empty sections.
- **Observability**: The `home.locality.explicit` log event now includes
  `isAuthenticated` so anonymous browse traffic is distinguishable from
  authenticated traffic without a separate event name.

No new endpoints were added. No existing authorization attributes were changed.

### Mobile

- **Boot flow** (`app/index.tsx`): Unauthenticated users now route to
  `/(app)/home` instead of `/(auth)/phone`. Both authenticated and
  unauthenticated users land on the home feed.
- **LocalityContext**: Added `setActiveLocality(locality)` to allow setting the
  active locality directly from a search result, bypassing the followed-
  localities lookup. This is used by anonymous browse where users have no
  followed wards.
- **Home screen**: Anonymous users see "Choose an area to explore" instead of
  "Follow a ward to see activity". The locality selector sheet allows anonymous
  users to select any search result for browsing (authenticated users still
  require followed localities).
- **FAB**: Tapping the Report FAB while unauthenticated navigates to auth
  instead of the composer.
- **Cluster detail**: Anonymous users see an `AuthPrompt` banner ("Sign in to
  report how this affects you") in place of the participation action buttons.
  The existing `requireAuth()` gate on participation handlers is preserved as
  a defense-in-depth measure.
- **AuthPrompt component**: New shared component for inline sign-in prompts
  shown during anonymous browse.

## Public vs protected surface decisions

### Public (no auth required)

| Endpoint / Surface | Rationale |
|---|---|
| `GET /v1/home?localityId=...` | Read-only feed data, no PII |
| `GET /v1/clusters/{id}` | Read-only cluster detail, no CIVIS internals |
| `GET /v1/localities/search` | Public locality lookup |
| `GET /v1/localities/resolve-by-coordinates` | Public geocoding |
| `POST /v1/signals/preview` | NLP preview, rate-limited per IP |
| Home feed screen | Browseable without auth |
| Cluster detail screen (read-only) | Viewable without auth |

### Protected (auth required)

| Endpoint / Surface | Enforcement |
|---|---|
| `POST /v1/signals/submit` | `[Authorize]` |
| `POST /v1/clusters/{id}/participation` | `[Authorize]` |
| `POST /v1/clusters/{id}/context` | `[Authorize]` |
| `POST /v1/clusters/{id}/restoration-response` | `[Authorize]` |
| `GET /v1/localities/followed` | `[Authorize]` (class-level) |
| `PUT /v1/localities/followed` | `[Authorize]` (class-level) |
| `GET /v1/users/me` | `[Authorize]` (class-level) |
| `PUT /v1/users/me/notification-settings` | `[Authorize]` (class-level) |
| `POST /v1/devices/push-token` | `[Authorize]` |
| Report FAB | Client-side gate to auth |
| Participation buttons | Client-side AuthPrompt + server `[Authorize]` |
| Settings screens | Require auth to access account data |

## Contract changes

None. The `?localityId` query parameter on `GET /v1/home` already existed; it
was just unnecessarily restricted to authenticated callers. The response shape
is unchanged.

## Known limitations

- Anonymous users cannot follow wards. They browse by selecting localities from
  the search interface, which creates a temporary browsing scope. This is
  intentional: following wards requires account state and is a B6/future concern.
- The locality selector "GPS opt-in" button does not resolve locality for
  anonymous users (GPS locality resolution is deferred to Phase G).
- Anonymous users do not see `myParticipation` on cluster detail (null from
  server). This is correct behavior.

## Follow-up implications

- **B6 (Installation-scoped identity)**: B8 does not create pseudo-accounts or
  silent device registrations. The anonymous browse session is purely stateless
  on the server. B6 can introduce device-scoped identity on top of this without
  conflicting with B8's posture.
- **B9 (Location label exposure)**: No B9 changes were needed for B8.
- **C12 (Analytics)**: The observability `isAuthenticated` field on home feed
  logs provides the basis for anonymous-vs-authenticated traffic segmentation
  without additional plumbing.

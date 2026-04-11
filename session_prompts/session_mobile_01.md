## Version: 1.0
## Last updated: 2026-04-05

# Session mobile-01 — Citizen Mobile App (Phase 1 MVP)
# Prerequisite: Backend sessions 00–07 complete. All 139 unit tests passing.

## Context
The full Phase 1 backend is complete: auth, signal intake, clustering, CIVIS, participation,
official updates, restoration lifecycle, notifications, and home feed. The API is running at
http://localhost:8080. This session builds the React Native citizen mobile app on top of it.

An initial scaffold already exists in apps/citizen-mobile/ from PR #28. Review what's there
before writing — extend and correct it rather than starting from scratch.

## Your task this session
Build the complete citizen mobile app, screen by screen, with full API integration.
The app must be buildable with `npx expo start` and functional against the running backend.

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.
These rules apply to every session: checkpoint commits, amnesia recovery,
prompt versioning, and context window management.

---

## Architecture authority
- Stack: React Native + Expo SDK, TypeScript strict, Expo Router, Expo SecureStore, TanStack Query, Zustand
- Full spec: docs/arch/04_phase1_mobile.md — this is your implementation contract
- API contracts: docs/arch/02_api_contracts.md — all endpoints the app calls
- Auth flow: docs/arch/07_auth_implementation.md — SMS OTP, JWT, refresh tokens

---

## Build order (mandatory sequence)

### Phase A — Auth flow
1. Phone entry screen: `app/(auth)/index.tsx`
   - Phone number input with +254 prefix (Kenya)
   - Calls POST /v1/auth/otp
   - On success → navigate to verify screen
2. OTP verification screen: `app/(auth)/verify.tsx`
   - 6-digit OTP input with auto-focus
   - Calls POST /v1/auth/verify
   - On success: store refreshToken in SecureStore, accessToken in memory
   - Register push token immediately after verify (POST /v1/devices/push-token)
   - Navigate to home feed
3. Auth context + token management: `lib/auth.ts`
   - In-memory access token, SecureStore refresh token
   - Silent refresh on 401: call POST /v1/auth/refresh, retry original request once
   - On refresh failure → clear session, redirect to (auth)/index
   - On app resume: if access token within 5 min of expiry, refresh silently

### Phase B — Home feed
4. Home feed screen: `app/(main)/index.tsx`
   - Calls GET /v1/home
   - Section order (canonical, do not change):
     1. Active now
     2. Official updates
     3. Recurring at this time
     4. Other active signals
   - Calm state: "Currently calm in [ward name]" with timestamp — NOT a generic empty illustration
   - Pull-to-refresh
   - Persistent Report FAB (bottom right) → opens composer step 1
   - Serve cached response when offline with "Last updated X ago" timestamp

### Phase C — Signal composer (3-step flow)
5. Step 1 — Free text input: `app/(main)/composer/step1.tsx`
   - Multiline text input, max 150 chars with character counter
   - Location section: current coarse location or "Search for a place" if denied
   - "Preview Signal" button → POST /v1/signals/preview
   - Loading: show skeleton, do not disable input
   - Error: inline error, do not navigate away
6. Step 2 — Confirm extraction: `app/(main)/composer/step2.tsx`
   - Show extracted: category (icon + label), subcategory, condition, location label
   - Location confidence gate:
     - >= 0.80 → pre-fill, no mandatory confirm
     - 0.50–0.79 → yellow "Confirm location" badge, MUST confirm/edit before next
     - < 0.50 → empty field with search, MUST fill before next
   - Condition confidence gate:
     - >= 0.75 → pre-fill
     - 0.50–0.74 → editable dropdown with extracted value pre-selected
     - < 0.50 → empty dropdown, MUST select
   - "Next" disabled until all mandatory confirmations satisfied
7. Step 3 — Join or create: `app/(main)/composer/step3.tsx`
   - If existingClusterCandidates: show up to 3 candidate cards with "Join this signal" CTA
   - "This is different — report separately" option at bottom
   - If no candidates: skip to "Report new signal" confirmation
   - Join: POST /v1/signals/submit with joinClusterId
   - Create: POST /v1/signals/submit with joinClusterId: null
   - Success → navigate to cluster detail, replacing composer stack
   - All submits use offline queue with client-generated idempotency key

### Phase D — Cluster detail
8. Cluster detail screen: `app/(main)/cluster/[id].tsx`
   - Calls GET /v1/clusters/{id}
   - Show: title, location, state badge, category, confirmation count
   - Official updates section (dual visibility — alongside, never replacing citizen content)
   - Actions based on state + participation:
     - No participation + active → "I'm Affected" (primary) + "I'm Observing" (secondary)
     - Affected → "Add Further Context" (2-min window only)
     - Affected + possible_restoration → restoration prompt CTA
     - Observing → "I'm Affected" upgrade action
   - Restoration progress bar: "72% of affected people say service is restored"
     (only when state = possible_restoration)
9. Participation actions:
   - POST /v1/clusters/{id}/participation (affected/observing)
   - POST /v1/clusters/{id}/context (further context, 2-min window)
   - POST /v1/clusters/{id}/restoration-response (yes/no restoration vote)
   - All use offline queue

### Phase E — Settings
10. Ward following: `app/(main)/settings/wards.tsx`
    - Show followed wards, count "N of 5 wards followed"
    - Disable Follow at capacity, show toast "You can follow up to 5 wards"
    - PUT /v1/localities/followed (bulk replace)
11. Notification settings: `app/(main)/settings/notifications.tsx`
    - PUT /v1/users/me/notification-settings
12. Account settings: `app/(main)/settings/account.tsx`
    - GET /v1/users/me
    - Logout → POST /v1/auth/logout, clear SecureStore, navigate to auth

### Phase F — Infrastructure
13. Offline queue: `lib/offline.ts`
    - Queue write operations when offline, flush on reconnect
    - Client-generated idempotency keys, reused on flush
    - User sees "Queued — will submit when connected" — not an error
14. Push notification deep links: in `app/_layout.tsx`
    - restoration_prompt → /(modals)/restoration/[clusterId]
    - cluster_activated_in_followed_ward → /(main)/cluster/[id]
    - cluster_resolved → /(main)/cluster/[id]
15. Location permission handling:
    - Request "when in use" on launch
    - If denied: ward picker on home, "Search for a place" in composer
    - Pass userEnteredPlace instead of lat/lng to preview endpoint

---

## API service layer
All API calls go through a typed service layer — not inline fetch() in components.
Base URL from `process.env.EXPO_PUBLIC_API_URL`.

```
lib/
  api.ts          — base fetch wrapper with auth + retry + offline queue
  auth.ts         — OTP request, verify, refresh, logout
  signals.ts      — preview, submit
  clusters.ts     — get by id, participate, context, restoration
  localities.ts   — followed wards (GET + PUT)
  users.ts        — me, notification settings
  devices.ts      — push token registration
```

---

## Hard rules
- Expo Router ONLY — do NOT use React Navigation
- SecureStore for refresh token — NEVER AsyncStorage, NEVER localStorage
- Access token in memory only — never persisted
- No map view of any kind
- No comments, replies, reactions, user profiles, media uploads
- No admin or institution screens
- No screens not in this inventory
- All timestamps displayed in local time (Africa/Nairobi), stored/sent in UTC
- Every screen handles three states: loading, error, success
- No hardcoded user-facing strings — use a strings.ts constants file
- Idempotency key in request body (not header) for all mutations

---

## Agent D contract output
At the end, produce:

```
AGENT_D_FRONTEND_CONTRACT:
Screens implemented:
  - [list every screen file with component name]
Service functions:
  - [every exported function with signature]
State shapes:
  - [every Zustand store and TanStack query key]
Navigation events:
  - [every navigation trigger]
```

## Agent A note
Agent A has no new backend work this session. If Agent A runs, it should verify
the existing backend compiles and all 139 tests pass — nothing else.

## Agent B note
Agent B should write tests for the mobile service layer (lib/*.ts) and any
pure utility functions. Target: all service functions have at least one test
for success and one for error path.

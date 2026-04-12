## Version: 1.0
## Last updated: 2026-04-05

# Session mobile-01e — Settings + Infrastructure
# Prerequisite: mobile-01d (cluster detail) complete.

## Context
All screens are built. This session completes settings screens, offline queue,
push token registration, and deep link routing.

## Your task this session
Build settings screens and cross-cutting infrastructure. This is the final mobile session.

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.

---

## Files in scope (MAX 4 production files)

1. **apps/citizen-mobile/app/(app)/settings/wards.tsx** — Ward following
   - Show currently followed wards (max 5)
   - Show count: "N of 5 wards followed"
   - If at capacity: disable "Follow" on search results, show "Manage your wards" prompt
   - Remove ward: swipe-to-delete or explicit remove button
   - PUT /v1/localities/followed replaces the full set — send all in one call
   - On 422 policy_blocked (max_followed_wards_reached): show toast
   - Search/add ward functionality

2. **apps/citizen-mobile/app/(app)/settings/notifications.tsx** — Notification settings
   - Toggle switches for: clusterActivated, restorationPrompt, clusterResolved
   - Calls PUT /v1/users/me/notification-settings on each toggle
   - Load current settings from GET /v1/users/me on mount

3. **apps/citizen-mobile/app/(app)/settings/account.tsx** — Account settings
   - Show: phone number (masked), account creation date
   - GET /v1/users/me on mount
   - Logout button: POST /v1/auth/logout, clear SecureStore, navigate to auth

4. **apps/citizen-mobile/src/lib/offline.ts** — Offline queue (NEW file)
   - enqueueOrSubmit(endpoint, body): check connectivity, queue if offline
   - flushOfflineQueue(): on reconnect, replay queued items
   - Client-generated idempotency key before queueing, reused on flush
   - User sees: "Queued — will submit when connected" — not an error state
   - Write paths that use queue:
     POST /v1/signals/submit
     POST /v1/clusters/{id}/participation
     POST /v1/clusters/{id}/restoration-response
     POST /v1/clusters/{id}/context
   - Queue stored in AsyncStorage (offline_queue key)
   - On server rejection (non-network error): drop item, don't retry

## Additional infrastructure (in existing files — audit + fix)

5. **apps/citizen-mobile/app/_layout.tsx** — Root layout (audit only)
   - Push notification deep links:
     restoration_prompt → /(modals)/restoration/[clusterId]
     cluster_activated_in_followed_ward → cluster/[id]
     cluster_resolved → cluster/[id]
   - Push token registration after auth bootstrap:
     Request Expo push permission
     If granted: getExpoPushTokenAsync() → POST /v1/devices/push-token
     If denied: skip silently
     On subsequent launches: re-register if token changed

6. **apps/citizen-mobile/src/api/localities.ts** — Ward API service (audit)
   - getFollowedLocalities(): GET /v1/localities/followed
   - setFollowedLocalities(body): PUT /v1/localities/followed

7. **apps/citizen-mobile/src/api/users.ts** — User API service (audit)
   - getMe(): GET /v1/users/me
   - updateNotificationSettings(body): PUT /v1/users/me/notification-settings

8. **apps/citizen-mobile/src/api/devices.ts** — Device API service (audit)
   - registerPushToken(body): POST /v1/devices/push-token

## Spec compliance checks
- Ward following: max 5, PUT replaces full set, 422 on over-limit
- Notification toggles: three switches matching NotificationSettings type
- Offline queue: idempotency keys generated before queue, reused on flush
- Push token: registered AFTER successful OTP verify, re-registered on change
- Deep links: three notification types mapped to correct routes
- Logout: clears SecureStore, calls API, navigates to auth

## Agent A note
Agent A: no new backend code. Produce AGENT_A_CONTRACT with settings + device endpoints.

## Agent B note
Agent B: write TypeScript tests (Vitest) at apps/citizen-mobile/__tests__/
Required test cases:
- getFollowedLocalities: success, empty
- setFollowedLocalities: success, max_followed_wards_reached (422)
- getMe: success
- updateNotificationSettings: success
- registerPushToken: success, error swallowed
- offline queue: enqueue when offline, flush on reconnect
- offline queue: idempotency key reused on retry
- offline queue: server rejection (422) drops item

## Agent D contract output
```
AGENT_D_FRONTEND_CONTRACT:
Screens: settings/wards.tsx, settings/notifications.tsx, settings/account.tsx
Services: localities.ts, users.ts, devices.ts, lib/offline.ts
Infrastructure: push deep links in _layout.tsx, offline queue
Navigation: logout → auth | deep link → cluster/[id] or restoration modal
```

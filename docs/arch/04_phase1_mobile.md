# Hali — Phase 1 Mobile Implementation Guide
**React Native + Expo citizen app — screen inventory, navigation, state management, and rules.**

Read `CLAUDE.md` and `docs/arch/02_api_contracts.md` before starting.

---

## Stack (mandatory — do not change)

```
React Native + Expo SDK (latest stable)
TypeScript (strict mode)
Expo Router (file-based routing)
Expo SecureStore (token storage)
Expo Notifications (push token registration)
Expo Location (coarse coordinates — not fine-grained tracking)
TanStack Query (server state / API calls)
Zustand (client-only state: auth session, composer draft)
```

---

## Folder structure

```
apps/citizen-mobile/
  app/
    (auth)/
      index.tsx          -- phone entry
      verify.tsx         -- OTP verification
    (main)/
      index.tsx          -- home feed (default tab)
      cluster/
        [id].tsx         -- cluster detail
      composer/
        step1.tsx        -- free text input
        step2.tsx        -- confirm extraction
        step3.tsx        -- join or create
      settings/
        wards.tsx        -- ward following
        notifications.tsx
        account.tsx
    (modals)/
      restoration/
        [clusterId].tsx  -- restoration prompt modal
      context/
        [clusterId].tsx  -- add further context modal
  components/
    feed/
    cluster/
    composer/
    shared/
  lib/
    api.ts               -- typed API client (generated from OpenAPI or hand-written)
    auth.ts              -- token management, refresh logic
    storage.ts           -- SecureStore wrapper
    offline.ts           -- offline queue
  hooks/
  constants/
    taxonomy.ts          -- import from /packages/taxonomy
```

---

## Navigation structure

```
Root:
  ├── (auth) stack — unauthenticated
  │   ├── index (phone entry)
  │   └── verify (OTP)
  │
  └── (main) tabs — authenticated
      ├── index (home feed)
      ├── [cluster/id] (cluster detail — pushed from feed)
      └── settings/* (pushed from home)

Modals (presented over main stack):
  └── (modals)/restoration/[clusterId]
  └── (modals)/context/[clusterId]

Composer (full-screen stack over main):
  └── composer/step1 → step2 → step3
```

---

## Auth session management

```typescript
// lib/auth.ts

export async function getAccessToken(): Promise<string | null> {
    // 1. Check in-memory cache first
    if (memoryToken && !isExpired(memoryToken)) return memoryToken;
    
    // 2. Try refresh
    const refreshToken = await SecureStore.getItemAsync('hali_refresh_token');
    if (!refreshToken) return null;
    
    try {
        const response = await api.post('/v1/auth/refresh', { refreshToken });
        await SecureStore.setItemAsync('hali_refresh_token', response.refreshToken);
        memoryToken = response.accessToken;
        return response.accessToken;
    } catch {
        // Refresh failed — clear session, redirect to auth
        await clearSession();
        return null;
    }
}

// On app resume (AppState change to 'active'):
// - Silently attempt token refresh if access token within 5 min of expiry
// - Do not block UI during refresh
```

---

## Offline queue

```typescript
// lib/offline.ts
// All write operations check connectivity and queue if offline

interface OfflineQueueItem {
    id: string;          // client-generated idempotency key
    endpoint: string;
    body: object;
    createdAt: string;
}

export async function enqueueOrSubmit(endpoint: string, body: Record<string, unknown>): Promise<void> {
    const idempotencyKey = generateIdempotencyKey();
    const requestBody = { ...body, idempotencyKey };
    const online = await NetInfo.fetch().then(s => s.isConnected);
    
    if (online) {
        await api.post(endpoint, requestBody);
        return;
    }
    
    // Queue locally
    const item: OfflineQueueItem = {
        id: idempotencyKey,
        endpoint,
        body: requestBody,
        createdAt: new Date().toISOString()
    };
    
    const existing = await AsyncStorage.getItem('offline_queue');
    const queue: OfflineQueueItem[] = existing ? JSON.parse(existing) : [];
    queue.push(item);
    await AsyncStorage.setItem('offline_queue', JSON.stringify(queue));
    
    // Show user feedback: "Queued — will submit when connected"
}

// On app resume / NetInfo connectivity change → connected:
export async function flushOfflineQueue(): Promise<void> {
    const existing = await AsyncStorage.getItem('offline_queue');
    if (!existing) return;
    
    const queue: OfflineQueueItem[] = JSON.parse(existing);
    const remaining: OfflineQueueItem[] = [];
    
    for (const item of queue) {
        try {
            await api.post(item.endpoint, { ...item.body, idempotencyKey: item.id });
        } catch (e) {
            if (!isNetworkError(e)) {
                // Server rejected it — don't retry (e.g., 422 policy_blocked)
                continue;
            }
            remaining.push(item);  // Network error — keep for retry
        }
    }
    
    await AsyncStorage.setItem('offline_queue', JSON.stringify(remaining));
}
```

Write paths that must use the offline queue:
- `POST /v1/signals/submit`
- `POST /v1/clusters/{id}/participation`
- `POST /v1/clusters/{id}/restoration-response`
- `POST /v1/clusters/{id}/context`

Read paths must serve cached last response when offline:
- `GET /v1/home`
- `GET /v1/clusters/{id}`

---

## Signal composer flow

### Step 1 — Free text input

```
Screen rules:
- Single text input, multiline, max 150 chars
- Character counter visible
- Location section below: shows current coarse location or "Set location"
- If location permission denied: show "Search for a place" input instead
- "Preview Signal" button calls POST /v1/signals/preview
- While loading: show skeleton — do not disable the input
- On error: show inline error — do not navigate away
```

### Step 2 — Confirm extraction

```
Screen rules:
- Show extracted: category (icon + label), subcategory, condition, location label
- Location confidence gate:
    >= 0.80 → show location with "Looks right? Continue" option — no mandatory confirm
    0.50–0.79 → show yellow "Confirm location" badge — MUST tap confirm or edit before Step 3
    < 0.50 → show empty location field with search input — MUST fill before Step 3
- Condition confidence gate:
    >= 0.75 → show condition pre-filled
    0.50–0.74 → show editable dropdown with extracted value pre-selected
    < 0.50 → show empty condition dropdown — MUST select before Step 3
- User can edit any field — edits invalidate the extracted value and mark as user-corrected
- "Next" button disabled until all mandatory confirmations are satisfied
```

### Step 3 — Join or create

```
Screen rules:
- If existingClusterCandidates returned from preview:
    Show up to 3 candidates as cards: summary, location, confirmation count, state
    "Join this signal" CTA on each
    "This is different — report separately" option at bottom
- If no candidates:
    Skip directly to "Report new signal" confirmation screen
- On join: POST /v1/signals/submit with { joinClusterId: clusterId, candidate: ... }
- On create: POST /v1/signals/submit with { joinClusterId: null, candidate: ... }
- On success: navigate to cluster detail screen, replacing composer stack
```

---

## Home feed

```
Screen rules:
- List-first, not map-first. No map in MVP.
- Section order is canonical and must not be changed:
    1. Active now
    2. Official updates
    3. Recurring at this time
    4. Other active signals
- Calm state: when no items in any section:
    Show: "Currently calm in [ward name]"
    Show: last checked timestamp
    Show: any upcoming scheduled disruptions (still render official updates section)
    Do NOT show a generic empty illustration
- Pull-to-refresh triggers GET /v1/home
- Persistent "Report" FAB visible at all times (bottom right)
  - Taps open Signal Composer Step 1 as a new stack
```

---

## Cluster detail

```
Screen rules:
- Show: title, location label, state badge, category, confirmation count
- Show: official updates linked to this cluster — in a separate "Official Response" section
  - Official updates displayed alongside, never replacing citizen content
- Show: user's current participation if any
- Actions based on state and user participation:
    No participation + cluster active:
      → "I'm Affected" (primary) + "I'm Observing" (secondary)
    User is "affected":
      → "Add Further Context" (only within 2-minute window)
      → If cluster in possible_restoration: restoration prompt CTA
    User is "observing":
      → "I'm Affected" (upgrade action)
    Cluster in possible_restoration:
      → Restoration prompt section with progress indicator
- Restoration progress: show ratio bar — e.g., "72% of affected people say service is restored"
  - Only show when state = possible_restoration
```

---

## Push notification deep links

```typescript
// In app/_layout.tsx:
Notifications.addNotificationResponseReceivedListener(response => {
    const data = response.notification.request.content.data;
    
    switch (data.notificationType) {
        case 'restoration_prompt':
            router.push(`/(modals)/restoration/${data.clusterId}`);
            break;
        case 'cluster_activated_in_followed_ward':
            router.push(`/(main)/cluster/${data.clusterId}`);
            break;
        case 'cluster_resolved':
            router.push(`/(main)/cluster/${data.clusterId}`);
            break;
    }
});
```

---

## Ward following

```
Screen rules:
- Show currently followed wards (max 5)
- Show count: "3 of 5 wards followed"
- If at capacity: disable "Follow" on search results — show "Manage your wards" prompt
- Remove ward: swipe-to-delete or explicit remove button
- PUT /v1/localities/followed replaces the full set — send all 5 (or fewer) in one call
- On 422 policy_blocked (max_followed_wards_reached): show toast "You can follow up to 5 wards"
```

---

## Push token registration

```
Register push token immediately after successful OTP verification:
  1. Request Expo push permission (if not already granted)
  2. If granted: const token = await Notifications.getExpoPushTokenAsync()
  3. POST /v1/devices/push-token { pushToken: token.data, platform: 'ios' | 'android' }
  4. On subsequent app launches: if stored token !== current token, re-register silently
  5. If user denies permission: skip silently — do not block onboarding
```

---

## Location permission handling

```
At app launch:
  - Request "when in use" location permission via Expo Location
  - If granted: use coarse coordinates for NLP context and locality detection
  - If denied: set locationAvailable = false in Zustand store
  
When locationAvailable = false:
  - Home feed: show ward picker ("Which area are you in?")
  - Signal composer step 1: show "Search for a place" input (no coordinate context)
  - Pass userEnteredPlace instead of lat/lng to POST /v1/signals/preview
  - NLP extracts location from text; server sets location_confidence based on text quality
```

---

## Do not build

- Map view of any kind
- Social feeds, activity feeds, or any notion of "posts" by users
- Comments, replies, reactions
- User profiles visible to other users
- Media uploads
- Admin or institution screens of any kind
- Any screen not listed in this inventory

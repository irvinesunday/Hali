## Version: 1.0
## Last updated: 2026-04-05

# Session mobile-01b — Home Feed
# Prerequisite: mobile-01a (auth stack) complete.

## Context
Auth stack is complete. This session builds the home feed screen with four canonical
sections, calm state, ward detection, and pull-to-refresh.

## Your task this session
Audit and complete the home feed screen and its supporting API/context layers.
Do NOT touch auth files or any screen outside the home feed scope.

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.

---

## Files in scope (MAX 4 production files)

1. **apps/citizen-mobile/app/(app)/home.tsx** — Home feed screen
   - Section order (canonical, must not change):
     1. Active now
     2. Official updates
     3. Recurring at this time
     4. Other active signals
   - Calm state (when all sections empty):
     Show: "Currently calm in [ward name]" with last checked timestamp
     Show: upcoming scheduled disruptions if any (still render official updates section)
     Do NOT show a generic empty illustration
   - Pull-to-refresh triggers GET /v1/home refetch
   - Persistent Report FAB (bottom right) → opens composer step 1
   - Ward selector pill in header — tap opens ward picker
   - Serve cached response when offline with "Last updated X ago" timestamp

2. **apps/citizen-mobile/src/api/clusters.ts** — Cluster API service
   - getHome(localityId?: string): Promise<HomeResponse> → GET /v1/home
   - getCluster(id: string): Promise<ClusterResponse> → GET /v1/clusters/{id}
   - participate, addContext, submitRestoration — stubs only this session

3. **apps/citizen-mobile/src/context/LocalityContext.tsx** — Ward state
   - activeLocalityId: string | null
   - followedLocalityIds: string[]
   - setActiveLocalityId, setFollowedLocalityIds

4. **apps/citizen-mobile/src/components/clusters/ClusterCard.tsx** — Feed card component
   - Shows: title, category icon, state badge, affected count, time ago
   - Tap navigates to cluster detail screen
   - Used in all four home feed sections

## Spec compliance checks
- Home API: GET /v1/home returns { activeNow, officialUpdates, recurringAtThisTime, otherActiveSignals }
- Calm state text: "Currently calm in [ward name]" — not generic empty state
- FAB always visible, even in calm state
- TanStack Query for data fetching with staleTime: 30_000
- Ward picker shows followed wards (max 5)

## Agent A note
Agent A: no new backend code. Produce AGENT_A_CONTRACT with GET /v1/home shape.

## Agent B note
Agent B: write TypeScript tests (Vitest) at apps/citizen-mobile/__tests__/
Required test cases:
- getHome: success with all four sections populated
- getHome: success with empty response (calm state)
- getHome: network error
- getFollowedLocalities: success, empty
- ClusterCard: renders title, state badge, affected count

## Agent D contract output
```
AGENT_D_FRONTEND_CONTRACT:
Screens: app/(app)/home.tsx
Components: ClusterCard, OfficialPostCard, Section
Services: src/api/clusters.ts (getHome, getCluster stubs)
State: LocalityContext (activeLocalityId, followedLocalityIds)
Navigation: FAB → composer/step1 | card tap → cluster/[id]
```

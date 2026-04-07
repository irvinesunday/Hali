## Version: 1.0
## Last updated: 2026-04-05

# Session mobile-01d — Cluster Detail
# Prerequisite: mobile-01c (signal composer) complete.

## Context
Auth, home feed, and signal composer are complete. This session builds the cluster
detail screen with participation actions, official updates panel, and restoration prompt.

## Your task this session
Build the cluster detail screen and its action modals. Do NOT touch auth, home feed,
or composer files.

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.

---

## Files in scope (MAX 4 production files)

1. **apps/citizen-mobile/app/(app)/clusters/[id].tsx** — Cluster detail screen
   - Calls GET /v1/clusters/{id}
   - Show: title, location label, state badge, category, confirmation count
   - Official updates section: separate "Official Response" area
     Official updates displayed alongside, never replacing citizen content
   - User's current participation shown if any
   - Actions based on state and user participation:
     No participation + cluster active → "I'm Affected" (primary) + "I'm Observing" (secondary)
     User is affected → "Add Further Context" (only within 2-minute window)
     User is affected + possible_restoration → restoration prompt CTA
     User is observing → "I'm Affected" upgrade action
   - Restoration progress bar: "72% of affected people say service is restored"
     Only visible when state = possible_restoration
   - Loading, error, success states

2. **apps/citizen-mobile/app/(app)/clusters/[id]/restore.tsx** — Restoration response modal
   - Presented when cluster is in possible_restoration and user was affected
   - Three options: "Service Restored for Me" / "Still Affected" / "Not Sure Yet"
   - Calls POST /v1/clusters/{id}/restoration-response
   - On success: refetch cluster detail, dismiss modal

3. **apps/citizen-mobile/src/api/clusters.ts** — Complete cluster API service
   - getCluster(id: string): Promise<ClusterDetailResponse> → GET /v1/clusters/{id}
   - participate(clusterId, req: ParticipationRequest): Promise<void>
     → POST /v1/clusters/{id}/participation
   - addContext(clusterId, req: ContextRequest): Promise<void>
     → POST /v1/clusters/{id}/context
   - submitRestorationResponse(clusterId, req: RestorationResponseRequest): Promise<void>
     → POST /v1/clusters/{id}/restoration-response

4. **apps/citizen-mobile/src/components/clusters/ParticipationBar.tsx** — Action bar
   - Renders appropriate buttons based on cluster state + user participation
   - "I'm Affected" primary button (green)
   - "I'm Observing" secondary button (outline)
   - "Add Further Context" — only visible within 2 min of I'm Affected action
   - Context requires: track participatedAt timestamp in component state

## Spec compliance checks
- GET /v1/clusters/{id} response includes officialPosts array
- Official updates shown alongside citizen content — never replacing
- "Add Further Context" only appears after I'm Affected, within 2-minute window
- Restoration progress: show percentage bar when state = possible_restoration
- All participation actions use idempotencyKey in request body
- No CIVIS internals (civis_score, wrab, sds, macf) in any display

## Agent A note
Agent A: no new backend code. Produce AGENT_A_CONTRACT with cluster detail + participation shapes.

## Agent B note
Agent B: write TypeScript tests (Vitest) at apps/citizen-mobile/__tests__/
Required test cases:
- getCluster: success with official posts, success without
- participate: affected, observing
- addContext: success, window expired (server returns 422)
- submitRestorationResponse: yes/no/unsure
- ParticipationBar: renders correct buttons for each state+participation combo
- 2-minute context window: button visible within window, hidden after

## Agent D contract output
```
AGENT_D_FRONTEND_CONTRACT:
Screens: clusters/[id].tsx, clusters/[id]/restore.tsx
Components: ParticipationBar, RestorationProgress, OfficialUpdatesPanel
Services: src/api/clusters.ts (getCluster, participate, addContext, submitRestorationResponse)
Navigation: participate → refetch | restore modal → dismiss | context → inline
```

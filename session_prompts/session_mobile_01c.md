## Version: 1.0
## Last updated: 2026-04-05

# Session mobile-01c — Signal Composer
# Prerequisite: mobile-01b (home feed) complete.

## Context
Auth and home feed are complete. This session builds the 3-step signal composer flow.
Each step is a separate screen — do NOT combine them.

## Your task this session
Build the signal composer: free text input, NLP confirmation with confidence gates,
and join-or-create decision. Do NOT touch auth or home feed files.

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.

---

## Files in scope (MAX 4 production files)

1. **apps/citizen-mobile/app/(app)/compose/text.tsx** — Step 1: Free text input
   - Single multiline text input, max 150 chars
   - Character counter visible
   - Location section: show current coarse location or "Search for a place" if denied
   - "Preview Signal" button → POST /v1/signals/preview
   - While loading: show skeleton — do not disable the input
   - On error: show inline error — do not navigate away
   - On success: navigate to step 2 with preview response

2. **apps/citizen-mobile/app/(app)/compose/confirm.tsx** — Step 2: Confirm extraction
   - Show extracted: category (icon + label), subcategory, condition, location label
   - Location confidence gate:
     >= 0.80 → pre-fill, no mandatory confirm
     0.50–0.79 → yellow "Confirm location" badge, MUST confirm or edit before step 3
     < 0.50 → empty field with search input, MUST fill before step 3
   - Condition confidence gate:
     >= 0.75 → pre-fill accepted
     0.50–0.74 → editable dropdown with extracted value pre-selected
     < 0.50 → empty dropdown, MUST select before step 3
   - User can edit any field — edits mark as user-corrected
   - "Next" button disabled until all mandatory confirmations satisfied

3. **apps/citizen-mobile/app/(app)/compose/submit.tsx** — Step 3: Join or create
   - If existingClusterCandidates from preview: show up to 3 candidate cards
     Each card: summary, location, confirmation count, state
     "Join this signal" CTA on each
     "This is different — report separately" option at bottom
   - If no candidates: skip directly to "Report new signal" confirmation
   - Join: POST /v1/signals/submit with { joinClusterId: clusterId, ... }
   - Create: POST /v1/signals/submit with { joinClusterId: null, ... }
   - On success: navigate to cluster detail, replacing composer stack
   - All submits use client-generated idempotency key

4. **apps/citizen-mobile/src/api/signals.ts** — Signal API service
   - previewSignal(req: SignalPreviewRequest): Promise<SignalPreviewResponse>
     → POST /v1/signals/preview
   - submitSignal(req: SignalSubmitRequest): Promise<SignalSubmitResponse>
     → POST /v1/signals/submit

## Supporting files (read, fix only if shapes are wrong)
- src/types/api.ts — SignalPreviewRequest, SignalPreviewResponse, SignalSubmitRequest
- src/context/ComposerContext.tsx — composer draft state (if exists)
- src/config/constants.ts — confidence thresholds

## Spec compliance checks
- Text input max 150 chars (constant in constants.ts)
- Confidence thresholds: location 0.80/0.50, condition 0.75/0.50
- Mandatory confirmation gates block "Next" until satisfied
- idempotencyKey generated client-side, included in submit request body (not header)
- On join: send joinClusterId; on create: send joinClusterId: null
- Success navigation replaces composer stack (router.replace, not router.push)

## Agent A note
Agent A: no new backend code. Produce AGENT_A_CONTRACT with preview + submit shapes.

## Agent B note
Agent B: write TypeScript tests (Vitest) at apps/citizen-mobile/__tests__/
Required test cases:
- previewSignal: success with candidates, success without candidates
- previewSignal: NLP unavailable error
- submitSignal: join existing cluster, create new cluster
- submitSignal: network error (queued for offline)
- Confidence gate logic: location >= 0.80 passes, 0.50-0.79 warns, < 0.50 blocks
- Confidence gate logic: condition >= 0.75 passes, 0.50-0.74 warns, < 0.50 blocks

## Agent D contract output
```
AGENT_D_FRONTEND_CONTRACT:
Screens: compose/text.tsx, compose/confirm.tsx, compose/submit.tsx
Services: src/api/signals.ts (previewSignal, submitSignal)
State: ComposerContext (freeText, previewResponse, userEdits)
Navigation: text → confirm → submit → cluster/[id] (replace)
```

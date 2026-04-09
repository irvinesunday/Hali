# Hali Mobile UI — UX Retrospective Review

_Against UI/UX Pro Max skill database_
_Generated: 2026-04-09_
_Stack: react-native | Hali Phase 1 MVP_

---

## Summary

The UI/UX Pro Max skill database is a generic cross-platform pattern catalogue
(forms, feedback, accessibility, list virtualization). It contains no civic-
or government-specific patterns and no domain knowledge of neutral signal
platforms. Where the skill speaks (loading states, mobile keyboards, form
labels, list virtualization, confirmation dialogs, multi-step progress
indicators) Hali's Phase 1 mobile screens already align HIGH. The
divergences against generic SaaS patterns (no infinite-scroll engagement
hooks, no rankings, list-led home, no onboarding tour) are doctrinal and
intentional. A small number of low-effort polish gaps were identified and
are listed at the end.

---

## Screen-by-screen review

### Home Feed — `app/(app)/home.tsx`

**Skill recommendations (ux):**
- Feedback › Loading Indicators — show spinner/skeleton for waits > 300ms (High)
- Forms › Submit Feedback — confirm submission status (High)
- Touch › Pull to Refresh — disable where not needed; otherwise be intentional (Low)
- Lists › Use FlatList for 50+ items, memoize renderItem, stable keyExtractor (High)

**Skill recommendations (style):**
- E-Ink / Paper — high contrast, calm, no motion blur, reading-focused (matches Hali's calm civic tone)
- Minimal & Direct — single column, generous whitespace, one CTA

**What Hali implements:**
- Four canonical sections (Active now → Official updates → Recurring → Other active) — order frozen by doctrine
- `useHome` query with `RefreshControl` pull-to-refresh; `lastUpdatedAt` freshness timestamp
- Calm state copy ("Currently calm in [ward]") instead of generic empty illustration
- Persistent Report FAB
- Locality switcher in header; guest mode supported read-only
- `Loading` component for in-flight, error fallbacks via `ApiResultError`

**Alignment:** HIGH

**Intentional divergences:**
- No infinite scroll, no engagement counters, no "trending" sort — Hali is list-led and neutral by doctrine (CLAUDE.md §"Non-negotiable product doctrine" #1, #4).
- No map view in Phase 1 — explicitly out of scope.
- Sections use `ScrollView` with bounded section pages rather than a single virtualized FlatList; acceptable because each section is paged server-side and capped.

**Genuine gaps (worth fixing):**
- Section item lists inside `home.tsx` should confirm `ClusterCard` is wrapped in `React.memo` and that section renders use stable keys (skill: Lists › Optimize renderItem, High). Verify in `ClusterCard.tsx` — if not memoized, add `React.memo` with shallow comparison.

---

### Cluster Detail — `app/(app)/clusters/[id].tsx`

**Skill recommendations (ux):**
- Feedback › Loading Indicators (High)
- Forms › Submit Feedback (High)
- Navigation › Back Button — preserve predictable back behaviour (High)

**What Hali implements:**
- Server-authoritative `myParticipation` (type + canAddContext); local state used only for button highlight between mutate→refetch
- `ParticipationBar` (I'm Affected / I'm Observing) with category-aware hiding of "Observing" for experiential categories (electricity, water)
- Add Further Context only after Affected, only inside the 2-min server-enforced window
- Restoration banner gated on `state === 'possible_restoration' && myParticipation.type === 'affected'`
- Official posts coexist alongside citizen content (dual visibility)
- `ClusterStateBadge` for lifecycle state, structured time formatting
- Auth wall: unauthenticated users navigated to auth on participate

**Alignment:** HIGH

**Intentional divergences:**
- No comment thread, no reactions, no share — doctrine ("Signals, not speech").
- No raw signal_event list shown — public sees the cluster object only.
- No civis_score / wrab / sds / macf / reason codes exposed — internal-only.

**Genuine gaps (worth fixing):**
- None identified. Confirm header back button uses `router.back()` (not `router.replace`) so navigation history is preserved (skill: Navigation › Back Button, High).

---

### Signal Composer — Step 1 `compose/text.tsx`

**Skill recommendations (ux):**
- Feedback › Progress Indicators — show "Step 2 of 4" for multi-step (Medium)
- Forms › Submit Feedback (High)
- Forms › Mobile Keyboards — `inputmode` per input type (Medium)
- Accessibility › Form Labels — never placeholder-only (High)

**What Hali implements:**
- Free-text input + optional location hint
- `MIN_TEXT_LENGTH = 10`, `SIGNAL_TEXT_MAX_LENGTH` enforced client-side
- `KeyboardAvoidingView` for iOS keyboard handling
- Calls `previewSignal` (NLP-first), navigates to `/compose/confirm`
- Idle / loading / error screen states with explicit error messaging
- Device fingerprint hash computed lazily on mount

**Alignment:** HIGH

**Intentional divergences:**
- 3-step flow (text → confirm → submit) with explicit screens, but no numeric "Step 1 of 3" indicator. The screen titles communicate progression instead.

**Genuine gaps (worth fixing):**
- LOW — add a lightweight `Step 1 of 3` indicator (or breadcrumb dots) at the top of each composer screen (skill: Progress Indicators, Medium severity).
- Verify both `TextInput`s have a visible label above the field, not placeholder-only (skill: Form Labels, High). If `text.tsx` and `confirm.tsx` rely solely on placeholder, add a small `<Text>` label above each field.

---

### Signal Composer — Step 2 `compose/confirm.tsx`

**What Hali implements:**
- NLP preview (category, subcategory, condition badge) editable
- `classifyLocationGate` derives `accept | confirm | required` from location confidence; `canProceed` enforces the gate before next
- `PreviewMissingFallback` if user lands here without preview state (deep-link safety)

**Alignment:** HIGH

**Intentional divergences:** None.

**Genuine gaps:** None identified — the confidence gate is exactly what the skill's "show system status" guidance implies, applied to NLP confidence rather than network latency.

---

### Signal Composer — Step 3 `compose/submit.tsx`

**What Hali implements:**
- `idempotencyKey` derived from `SHA256(freeText:timestamp)` and sent in body (matches CLAUDE.md rule: idempotency in body, not header)
- Loading / error states; `reset()` and `router.replace('/(app)/home')` on success
- Final summary screen before commit

**Alignment:** HIGH

**Genuine gaps:** None identified.

---

### Auth — Phone Entry `app/(auth)/phone.tsx`

**Skill recommendations (ux):**
- Forms › Mobile Keyboards — `inputmode='numeric'` (Medium)
- Forms › Submit Feedback (High)
- Content › Number Formatting (Low)

**What Hali implements:**
- Kenya `+254` prefix, `normaliseKenyaPhone` + `isValidKenyaPhoneInput` validation
- `KeyboardAvoidingView`, idle/loading/error states
- POST `/v1/auth/otp` then route to OTP screen with destination param
- Submit disabled until input is valid

**Alignment:** HIGH

**Genuine gaps (worth fixing):**
- Verify the phone `TextInput` sets `keyboardType="phone-pad"` (RN equivalent of `inputmode='tel'`). If not, add it — skill High severity equivalent.

---

### Auth — OTP Verification `app/(auth)/otp.tsx`

**What Hali implements:**
- 6-digit code, auto-submit on 6th digit
- `keyboardType="number-pad"` expected; idle/loading/error states
- Device fingerprint hash on verify
- POST `/v1/auth/verify` → `signIn()` → `router.replace('/(app)/home')`

**Alignment:** HIGH

**Genuine gaps (worth fixing):**
- LOW — confirm `textContentType="oneTimeCode"` (iOS) and `autoComplete="sms-otp"` (Android) are set so the OS keyboard surfaces the SMS code. This is a real usability win even though the skill DB doesn't list it directly.

---

### Settings — Ward Following `app/(app)/settings/wards.tsx`

**What Hali implements:**
- View / search / add / remove with hard cap of 5 (`MAX_FOLLOWED_WARDS`); search disabled at capacity
- PUT `/v1/localities/followed` replaces full set in one call
- GPS opt-in flow gated by `FEATURE_GPS_LOCALITY_OPT_IN` flag
- Optimistic UI via React Query mutate
- Server-side enforcement (422 max_followed_localities_exceeded) as the source of truth

**Alignment:** HIGH

**Intentional divergences:**
- Cap is 5, hard-coded by doctrine. No upsell, no "go pro for more wards."

**Genuine gaps:** None identified.

---

### Settings — Notifications `app/(app)/settings/notifications.tsx`

**Skill recommendations (ux):**
- Animation › Reduced Motion (High, but mostly web)
- Forms › Mobile Keyboards (N/A here)

**What Hali implements:**
- Push enable/disable with re-trigger of registration if previously denied
- Per-type `Switch`es: clusterActivated / restorationPrompt / clusterResolved
- PUT `/v1/users/me/notification-settings` per change with optimistic update + rollback on failure
- React Query mutation pattern consistent with rest of app

**Alignment:** HIGH

**Genuine gaps:** None identified.

---

### Restoration Modal — `app/(modals)/restoration/[clusterId].tsx`

**Skill recommendations (ux):**
- Interaction › Confirmation Dialogs — confirm before irreversible actions (High)
- Feedback › Confirmation Messages — brief success on action (Medium)

**What Hali implements:**
- Three options: `restored` / `still_affected` / `not_sure` mapped to backend `ParticipationType.RestorationYes` / `Affected` / `RestorationUnsure`
- Idle/loading/error states; idempotent server submit
- Modal presentation, dismissible; option helper text per choice

**Alignment:** HIGH

**Intentional divergences:**
- No "No, not restored" option — doctrine: restoration is confirmation-based, not a binary tally. The negative case is expressed as "still affected" which re-records the user as currently affected.

**Genuine gaps (worth fixing):**
- LOW — after the action completes, surface a brief toast/inline confirmation ("Thanks — recorded") before dismissing the modal (skill: Confirmation Messages, Medium).

---

## Cross-cutting patterns

### Anti-patterns check
The "civic government public service mobile" query returned generic items
(mobile keyboards, mobile-first, pull-to-refresh, table responsiveness, back
button). None of these flagged Hali — the app is mobile-native, has no
tables, and uses Expo Router which preserves stack history correctly.

### Patterns Hali already follows well
1. **Server-authoritative state** — `myParticipation` from API, idempotency keys in mutation bodies, optimistic UI with rollback.
2. **Explicit screen states** — every async screen tracks `idle | loading | error` rather than juggling booleans.
3. **Calm, list-led visual language** — aligns with the skill's "E-Ink / Paper" and "Minimal & Direct" style families.
4. **Doctrinal deep-link safety** — composer screens render `PreviewMissingFallback` if state is missing (deep-link or process-restart).
5. **Hard caps enforced both client- and server-side** — followed wards (5), context window (2 min), text length, restoration thresholds.
6. **Categorical UX gating** — "Observing" hidden for experiential categories; gate is data-driven, not hard-coded per screen.

### Recommended improvements (prioritised)

| Priority | Screen | Issue | Recommended fix | Effort |
|---|---|---|---|---|
| HIGH | `(auth)/phone.tsx` | Phone input may use default keyboard | Set `keyboardType="phone-pad"` and `textContentType="telephoneNumber"` | XS |
| HIGH | `(auth)/otp.tsx` | OTP autofill from SMS not guaranteed | Set `textContentType="oneTimeCode"` (iOS) + `autoComplete="sms-otp"` (Android) | XS |
| HIGH | `compose/text.tsx`, `compose/confirm.tsx` | Inputs may be placeholder-only | Add a visible `<Text>` label above each `TextInput` | S |
| MEDIUM | `compose/*` | No multi-step progress indicator | Add simple `Step n of 3` header or 3-dot indicator in composer screens | S |
| MEDIUM | `feed/ClusterCard.tsx` | List item may not be memoized | Wrap in `React.memo` with shallow comparison if not already | XS |
| LOW | `(modals)/restoration/[clusterId].tsx` | Silent success on submit | Brief confirmation toast/text before dismiss | XS |
| LOW | `home.tsx` | Pull-to-refresh always on | Confirm `RefreshControl` is intentional and bounded; already is — leave as-is | — |

Priority rules:
- HIGH = affects usability or accessibility, easy to fix
- MEDIUM = UX improvement, low effort
- LOW = polish, cosmetic, deferrable

---

## Phase 2 readiness notes

Before designing the institution dashboard screens (Phase 2), run these
skill queries against the `nextjs` / `shadcn` stacks (web), not `react-native`:

```bash
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "data table sortable filterable dashboard" --domain ux -n 5
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "operational dashboard real-time monitoring" --domain ux -n 5
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "dashboard kpi cards layout" --stack shadcn -n 5
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "form composition validation server" --stack nextjs -n 5
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "role based access permission ui" --domain ux -n 5
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "audit log timeline activity feed" --domain ux -n 5
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "calm professional government dashboard" --domain style -n 3
python3 .claude/skills/ui-ux-pro-max/scripts/search.py "data table mobile responsive" --domain ux -n 3
```

The skill's `shadcn` and `nextjs` stack files are richer than `react-native`
for dashboard patterns and should be the primary references for institution
operator and admin screens.

---

## What the skill does NOT apply to Hali

| Skill recommendation | Why rejected for Hali |
|---|---|
| Engagement-driven feeds (infinite scroll, trending, "for you") | Doctrine #1 Neutrality, #4 Signals not speech — no rankings or engagement loops. |
| Comments / reactions / share buttons on detail screens | Doctrine #4 — structured civic input only, no discussion threads. |
| Social profile pages, follower graphs, identity badges | Doctrine #5 — anonymous participation; identity never exposed publicly. |
| Showing per-item confidence scores or trust meters | Doctrine #6 — CIVIS internals (civis_score, wrab, sds, macf) never leave trusted surfaces. |
| Map-first home screen | Phase 1 doctrine — list-led, no map. |
| Onboarding tour / coach marks | Out of scope for Phase 1; calm-state and persistent Report FAB are the only surface affordances. |
| Gamification, streaks, leaderboards | Doctrine #1 + Phase 1 out-of-scope list. |
| Generic "thumbs up / thumbs down" feedback on official posts | Dual visibility doctrine — citizens cannot rate institution posts; participation is only on clusters. |
| Toast for every micro-interaction | Hali prefers in-place state changes over notification chrome to keep the UI calm. |
| Numeric "X people affected" prominently surfaced | Counts exist server-side but are de-emphasised on public surfaces to avoid implying severity rankings. |

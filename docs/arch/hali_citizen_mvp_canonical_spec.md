# Hali Citizen Mobile App — Canonical MVP UI/UX & Architecture Spec
**Version:** Freeze Edition v1.1
**Status:** Locked for MVP prototype and engineering alignment
**Scope:** Citizen mobile app only
**Purpose:** Consolidated canonical specification for the Hali citizen experience, merging original architecture doctrine with lessons validated through v0 MVP prototyping.
**Supersedes:** v1.0 of this canonical spec

**Changelog from v1.0:**
- §10.3: Character limit clarified as frontend validation only; DB schema must not hardcode this value.
- §7.5, §10.6, §10.7: Add Further Context is available after both I'm Affected and I'm Observing.
- §9.1: Lifecycle states split into domain states vs. UI presentation states with a derivation map.
- §13.3: Max followed areas cap (5) made explicit. "Your Areas" locked as canonical screen name.
- §20 (new): UI Implementation Notes from v0 prototype screenshots, including component anatomy, color system, and known prototype bugs.

---

## 1. Purpose of This Document

This file is the canonical reference for the **Hali citizen mobile app MVP**.

It defines:

- Product doctrine
- UX principles
- Interaction rules
- Signal lifecycle behavior
- Demo simulation behavior
- Mobile UI decisions validated through prototyping

Claude, engineers, designers, and future product contributors should treat this file as the source of truth when implementing or modifying the Hali citizen app.

This document is intentionally focused on the **citizen app only**.
Institution dashboard architecture should be maintained separately.

---

## 2. Product Identity

### 2.1 What Hali Is

Hali is a **Civic Signal Infrastructure**.

It is not:

- A social network
- A complaints platform
- A generic reporting tool
- A public shaming system

It is:

- A real-time civic visibility layer
- A locality-based signal system
- A convergence medium between citizen experience and institutional response
- A civic weather system for everyday conditions

### 2.2 Civic Weather Model

Hali behaves like a weather system:

- **Signals** = conditions
- **Institutions** = advisories / updates
- **Time** = evolution
- **Resolution** = stabilization confirmed by lived experience

Hali does not fix problems.
Hali reveals conditions.

### 2.3 Core Loop (Invariant)

Every feature must support the civic feedback loop:

**Signal → Visibility → Response → Resolution → Confirmation**

If a proposed feature does not improve this loop, it should be rejected.

---

## 3. Core Doctrine

### 3.1 Neutrality

Hali must not:

- Blame institutions
- Judge performance publicly
- Rank agencies
- Amplify outrage
- Moderate sentiment as a social platform

Hali must only:

- Display citizen signals
- Display official updates
- Show progression over time
- Show convergence or divergence between lived experience and official communication

### 3.2 Trust and Legibility

The system must feel:

- Neutral
- Readable
- Immediate
- Calm
- Trustworthy

Users should understand what is happening in seconds, not minutes.

### 3.3 Alive, Never Static

Hali must feel alive.

The citizen experience should not feel like static mock data.
Even in demo mode, the UI should show:

- New signals appearing
- Existing counts changing
- Trends evolving
- Official updates arriving later, not instantly
- Signals moving gradually toward restoration

---

## 4. Citizen UX Principles (Non-Negotiable)

### 4.1 Zero Cognitive Load

The citizen app must:

- Be understandable instantly
- Require no onboarding to grasp the main value
- Support one-tap participation where possible
- Use plain, human-readable language

### 4.2 Fast Interpretation

At a glance, the user should know:

- What is happening
- Where it is happening
- Whether it is growing or calming
- Whether authorities have responded
- Whether restoration may be underway

### 4.3 Human, Not Bureaucratic

Interaction language should feel human and simple.

Preferred framing:
- "What's the Hali?"
- "I'm affected"
- "I'm observing"
- "Has service been restored for you?"

Avoid bureaucratic language like:
- "Submit report"
- "Incident classification"
- "Stage state management"

### 4.4 Action Before Detail

The user should be able to:
- understand the issue first
- act second
- inspect details third

Signal cards should remain concise; signal detail pages provide deeper context.

---

## 5. Information Architecture

The citizen app MVP includes:

1. **Home**
2. **Signal Detail**
3. **Report / NLP Composer Modal**
4. **Your Areas**
5. **Profile**
6. **Preferences / basic settings**
7. **Demo controls** (prototype only)

Bottom navigation must include:

- Home
- Your Areas
- Profile

A floating **+** action button is allowed globally for signal creation.

---

## 6. Home Screen Specification

### 6.1 Header

Home header must include:

- Hali wordmark / title
- Current locality selector
- Locality state line under the selected area
- Optional subtle prompt: **"What's the Hali?"**

The current locality selector should:
- be tappable
- open a modal / dropdown list of followed areas
- allow switching to another followed area
- update the feed for the selected locality

### 6.2 Locality Subtitle

Under the current area, show a state line such as:

- Currently calm
- Elevated activity
- 2 active signals

This line should reflect the selected area's current state.

### 6.3 Calm State

If no active signals exist for the selected locality:

Show:
- calm empty state
- "Currently calm"
- "Last checked X mins ago"

Do **not** show the "ACTIVE NOW" section when there are no active signals.

### 6.4 Active State

If signals exist:

Show section:
- **ACTIVE NOW**

Cards should be ordered dynamically by priority, generally favoring:

1. Growing
2. Stable but high-impact
3. Slowing
4. Possible restoration

Priority ordering must feel alive and dynamic.

### 6.5 Signal Cards

Each signal card must include:

- Title (clear and human-readable)
- Icon / category indicator
- Condition chip
- Participation count
- Ongoing duration
- Secondary trend/status line
- Optional official-response hint
- LIVE marker when applicable

Examples of secondary lines:
- Reports increasing
- Widely reported
- Being addressed
- Possible restoration
- Reports slowing

Signal cards must be clickable and open the signal detail page.

### 6.6 Official Updates Layer on Home

Official updates may appear below active signals as a separate, lighter-weight section.

Rules:
- Citizen signals remain primary
- Official updates must not visually overpower signals
- Official updates are tappable
- Opening an official update shows full text in a modal
- Tapping outside the modal closes it

Official updates on the home screen must:
- expire / rotate
- not persist indefinitely after the related signal is calm or resolved
- feel reactive to what is happening in the system

---

## 7. Signal Detail Page Specification

### 7.1 Structure

Signal detail page must include:

- Back navigation
- LIVE / state marker
- Icon
- Human-readable title
- Location
- Condition chip
- Participation count
- Ongoing duration
- Secondary status line
- Situation Details card
- Official Update card / timeline item
- Restoration Status section

### 7.2 Situation Details Card

Must include:

- What people are reporting
- Current condition
- Started / duration reference
- Location

This card must always align with the signal's actual category and condition.
No mixed issue/category content is allowed.

### 7.3 Official Update Section

If an official update exists, it should appear clearly as a parallel layer.

Rules:
- Must match the signal's actual category and institution
- Must not show the wrong institution
- Must not reference a different issue type
- Should be tappable if shown as compact card
- Expanded content must be readable, scrollable, and padded

### 7.4 Restoration Status Section

Used only when appropriate.

For experiential service signals (power, water, etc.), once official restoration is reported or possible restoration is inferred, show:

**Prompt:**
"Has service been restored for you?"

Buttons:
- Yes, Restored
- No, Still affected
- Not sure

Rules:
- This confirmation flow must persist per signal state, not reset incorrectly on simple navigation
- Official restoration should not auto-close the signal without citizen-side confirmation patterns
- If no restoration is in play, the section should communicate monitoring status instead of showing premature restoration prompts

### 7.5 Participation Controls by Signal Type

This is critical.

#### Experiential signals
Examples:
- No power
- Water disruption
- Unstable power

Allowed:
- I'm affected

Not allowed:
- I'm observing

Reason:
These are lived-impact states, not purely observational.

#### Observational signals
Examples:
- Flooding
- Potholes
- Lane blockage
- Garbage piling up
- Noise in public area

Allowed:
- I'm affected
- I'm observing

Reason:
A person may witness the condition without being directly affected.

### 7.6 Reporter State

If the current user created the signal:
- do not present the raw default participation buttons as if they had not already acted
- show a confirmation state like:
  - "You reported this"
  - "You're automatically marked as affected"

If the user later joins as observing or affected on an existing signal:
- the selected state must show clearly
- toggling off should be allowed when appropriate
- feedback text like "Thanks for the update" should fade after a short duration rather than linger forever

---

## 8. Participation Model

### 8.1 Participation Actions

Citizen participation actions are:

- I'm affected
- I'm observing

### 8.2 Rules

Both actions must visibly increase the public participation count.

Internal logic may weight them differently, but the UI must show that both matter.

- Affected = higher internal weight
- Observing = lower internal weight but still visible and count-incrementing

### 8.3 Opt-out / Removal

Users should be able to remove their participation state.

Examples:
- "I'm affected (tap to remove)"
- "I'm observing (tap to remove)"

### 8.4 Counts Must Persist

Counts must:
- update immediately when the user participates
- remain updated when navigating back to home
- remain consistent when reopening the signal
- not reset unexpectedly

---

## 9. Signal Lifecycle (Citizen View)

### 9.1 Lifecycle States — Two Layers

Signal lifecycle operates across two distinct layers that must not be confused:

#### Layer A — Backend Domain States (state machine)

These are the authoritative states persisted and managed by the backend:

| Domain State | Meaning |
|---|---|
| `unconfirmed` | Insufficient evidence for broad visibility |
| `active` | Current condition has enough evidence and recency |
| `possible_restoration` | Recovery proposed but not yet confirmed |
| `resolved` | Condition has crossed recovery threshold or decayed with adequate confidence |

These are the only values the backend stores in `signal_clusters.state`.

> **Note:** The backend `SignalState` enum may include additional values (e.g. `expired`, `suppressed`) used for internal processing. The table above documents only the citizen-app-relevant subset of states that drive UI presentation.

#### Layer B — UI Presentation States (frontend-derived)

These are display-layer labels derived from backend data at render time. They are never stored as domain state:

| UI Label | Derived From |
|---|---|
| **Unconfirmed** | domain state = `unconfirmed` |
| **Active** | domain state = `active`, participation velocity neutral |
| **Growing** | domain state = `active` + participation count increasing in recent window |
| **Stable** | domain state = `active` + participation count steady / plateau |
| **Slowing** | domain state = `active` + participation count declining |
| **Possible Restoration** | domain state = `possible_restoration` |
| **Resolved** | domain state = `resolved` |

**Engineering rule:** Growing, Stable, and Slowing are computed by the frontend (or a lightweight API projection field) based on recent participation velocity. The backend domain state remains `active` for all three. Do not introduce these as state machine values in the database.

**Suggested API projection field:** Include an optional `trend` field in the cluster response with values `growing | stable | slowing | null`. The frontend uses this to select the appropriate display label and card secondary line.

### 9.2 Unconfirmed State

A newly created signal may begin as **Unconfirmed**.

Rules:
- In production logic, single-report signals should not dominate the main feed until thresholding is satisfied
- For demo MVP, unconfirmed signals may still surface in a controlled way so the prototype feels alive
- Unconfirmed cards must clearly indicate low confirmation state

### 9.3 Transition to Active

Signals move from Unconfirmed to Active when participation / simulation threshold is met.

For demo:
- after a short delay, nearby simulated participants should join
- count should rise visibly
- state should shift into active/growing when appropriate

### 9.4 Growth and Decay

Signals must:
- grow gradually
- stabilize
- slow down
- decay gradually

They must not disappear abruptly.

### 9.5 Minimum Persistence

Signals should remain visible long enough to feel meaningful.

For demo baseline:
- avoid disappearing in under ~8–10 minutes of demo time unless explicitly simulating a rapid resolution
- use gradual decline instead of sudden removal

### 9.6 Resolution

Resolution should happen gradually and legibly:

- citizen reports slow down
- official updates may progress
- possible restoration appears
- citizens confirm restoration
- signal eventually resolves

Not all signals should receive official intervention.

Some should resolve through citizen-led confirmation or natural decay.

---

## 10. NLP Composer / Report Flow

### 10.1 Entry Point

The report flow is launched via the floating **+** button.

### 10.2 Header Copy

Use:
- **"What's the Hali?"**

### 10.3 Input

The input field should support short natural language reporting.

Helper text:
- "Describe what's happening… (keep it short and clear)"

Character limit:
- **150 characters max — enforced at the frontend/UI layer only.**
- The database schema must NOT hardcode this value. The `free_text` column should use `text` or a generous `varchar` to allow future flexibility without a migration.
- The 150-character limit is a product decision about citizen reporting UX, not a storage constraint.

### 10.4 Debounced Inference

Inference must be debounced at roughly **300–500ms**.

Required:
- no flickering between states
- no duplicate inference windows
- no unstable layout switching while typing

### 10.5 NLP Output

The composer should infer and display:

- Suggested category
- Suggested condition
- Suggested location (if possible)
- Similar nearby signal, if found

### 10.6 Join Existing Signal Flow

If a matching signal exists:
- show a suggested existing signal card
- show context-sensitive action buttons:
  - I'm affected
  - I'm observing (only for observational signal types; see §7.5)
- allow "Create new signal instead"

After joining an existing signal via I'm Affected:
- show lightweight confirmation
- reveal **Add Further Context** (see §10.7)

After joining via I'm Observing:
- show lightweight confirmation only (no further context option)

### 10.7 Add Further Context

After joining an existing signal via I'm Affected or after creating a new signal, the user may optionally add more detail. This option is **not** available after I'm Observing — only affected participants may add context.

This is:
- optional
- short
- capped at 150 chars (enforced at both the frontend for UX and the database level via `context_text varchar(150)` schema constraint)
- available only after I'm Affected, within the 2-minute edit window

Useful quick suggestions may be shown, e.g.:
- Started recently
- Ongoing for hours
- Happens often
- Worse at night
- Improving

### 10.8 Create New Signal Flow

If no suitable match exists:
- allow creation of a new signal
- show a confirmation screen / review state if needed
- auto-mark the reporter as affected where relevant
- insert the new signal into the appropriate locality feed

### 10.9 Location Resolution

Location must be explicit and human-readable.

Use:
- road + landmark / junction where applicable
- area-level label for experiential utility issues

Avoid malformed or clipped location strings.

Examples:
- South B near Capital Centre
- Lusaka Rd / Uhuru Hwy intersection
- Nyayo Stadium roundabout

---

## 11. Supported Citizen Signal Types for MVP

Citizen MVP should support a meaningful, bounded set of categories.

### 11.1 Experiential
- Power outage
- Power fluctuation / unstable power
- Water outage
- Water disruption

### 11.2 Observational
- Flooding
- Potholes / road condition
- Lane blockage / traffic obstruction
- Garbage piling up / waste disruption
- Noise / pollution in public area

### 11.3 Demo Rule

Majority of signals in the demo should originate from citizens, not institutions.

The app should not feel like an institution-led update feed.

---

## 12. Demo Simulation Behavior (Citizen App)

This section is critical because the MVP is a demo prototype, not a production live system.

### 12.1 Goals

The demo must feel:
- alive
- believable
- active within 60 seconds
- grounded in locality-specific civic conditions

### 12.2 Required Simulation Behavior

The system should simulate:

- new signals appearing every few seconds
- existing signal counts increasing
- some signals remaining citizen-only for a while
- some signals later receiving official updates
- some signals moving to possible restoration
- some signals resolving
- some areas remaining calm while others become elevated

### 12.3 Signal Arrival Cadence

The citizen app must not feel empty for long stretches.

Within 1–2 minutes of demo viewing:
- at least some new signals or updates should appear
- selected areas should visibly change
- user-created signals should attract simulated confirmations after a short delay

### 12.4 Participation Simulation

When a new signal is created:
- simulated nearby users should sometimes confirm it after a few seconds
- count should rise
- state should move from unconfirmed to active when appropriate

Existing active signals should also receive periodic participation changes.

### 12.5 Time Compression

Demo time may be accelerated, but it must remain believable.

Avoid:
- everything showing exactly ~45 min
- instant restoration with no buildup
- abrupt state shifts

Use variable durations:
- ~10 min
- ~25 min
- ~1 hr
- ~2 hrs
- ~3 hrs

### 12.6 Official Update Timing

Not all signals should get immediate official updates.

Use realistic delay patterns:
- some receive no official response
- some are acknowledged later
- some get mid-stream updates
- some reach official restoration

### 12.7 Official Update Progression

When official updates do happen, they should feel progressive.

Examples:
- Investigating
- Teams dispatched / on site
- Work ongoing
- Restoration in progress
- Service restored

### 12.8 Dynamic Area State

Followed areas should dynamically change between:
- Currently calm
- Elevated activity
- Active signal count

Also include:
- Last checked X mins ago

---

## 13. Your Areas Screen

**"Your Areas" is the locked canonical name for this screen.** Do not rename it to "Ward Following Settings" or any other label in code, navigation, or docs.

### 13.1 Purpose

This screen is for locality browsing and management.

It should show:
- followed areas
- current area
- number of active signals or calm state
- last checked timestamp

### 13.2 Behavior

Each area row must:
- be tappable
- navigate to that area's home/locality feed
- show whether it is current (display a CURRENT badge)
- show a follow/unfollow control

### 13.3 Follow / Unfollow

Users must be able to:
- add an area
- unfollow an area

**Maximum followed areas: 5.** The UI must prevent adding a 6th area. Show a clear message if the cap is reached.

The add-area modal must not be obscured by the floating action button.

### 13.4 Last Checked

Area cards should show:
- Last checked just now
- Last checked ~2 min ago
- Last checked ~5 min ago

This should feel dynamic, not frozen.

---

## 14. Profile Screen

### 14.1 Signed-out State

Show:
- "You"
- "Not signed in"
- Sign in / Create account action

Important:
Following areas may be allowed on-device without sign-in for MVP demo.

If unsigned:
- show that areas are saved on this device
- explain that sign-in allows cross-device continuity

### 14.2 Signed-in State

Show:
- Name placeholder (e.g. John Doe in demo)
- "Anonymous participation enabled"

### 14.3 Followed Areas in Profile

Profile may also surface followed areas.

This is a secondary view of area management and should remain consistent with the Your Areas screen.

### 14.4 Demo Controls

For demo builds, profile may include:
- Reset demo data

This is allowed for MVP prototype use.

---

## 15. Official Updates in Citizen App

### 15.1 Role

Official updates are a parallel truth layer, not a replacement for citizen signals.

### 15.2 Rules

Official updates must:
- align with the actual signal category
- align with the correct institution
- not outlive the signal context unrealistically
- expire or rotate after relevant time

### 15.3 Modal Behavior

Opening an official update should:
- show full readable message
- be scrollable if needed
- close on backdrop tap
- not require the user to hit only the X button

### 15.4 No Static Zombie Updates

If an area is calm, outdated official updates for unrelated issues should not remain permanently visible.

Official updates must react to the demo state.

---

## 16. Copy and Brand Language

### 16.1 Brand Layer

Use:
- Hali
- What's the Hali?

### 16.2 Functional Explanations

Use supporting language such as:
- Hali shows what's happening in your area and what authorities are doing about it.
- Describe what's happening… (keep it short and clear)

### 16.3 Tone

Tone should be:
- calm
- human
- concise
- neutral
- lightly conversational, never playful in a way that undermines trust

---

## 17. Visual Design Rules

### 17.1 Design Character

The citizen app should feel:
- calm
- modern
- civic
- trustworthy
- soft rather than aggressive

### 17.2 Visual Pattern

Use:
- soft rounded cards
- subtle shadows
- teal accent system
- light neutral background
- clear hierarchy

### 17.3 Modals

Modals must:
- be scrollable
- maintain safe margins
- not truncate text
- not allow floating controls to obscure key content
- remain usable on mobile viewport with keyboard open

---

## 18. Known MVP Decisions Locked from Prototyping

The following are explicitly locked based on v0 prototype learning:

1. **"What's the Hali?"** is the correct citizen-side branded prompt.
2. Signal participation must distinguish experiential vs observational issues.
3. Observing must visibly increment public participation.
4. New signals should attract simulated confirmations after a short delay.
5. Official updates should be reactive, not static.
6. Signal decay must be gradual, not abrupt.
7. Followed areas must show dynamic state and last-checked status.
8. Users must be able to add and unfollow areas.
9. Official update modals should close on outside tap.
10. The app must remain compelling within 60 seconds of viewing.

---

## 19. What Must Never Happen

- No duplicated inference windows in NLP composer
- No flickering UI while typing
- No mixed category/institution data inside a signal
- No static empty experience for long stretches
- No abrupt signal disappearance
- No official updates dominating citizen reality
- No wrong institution attached to the wrong signal
- No participation buttons that contradict the signal type
- No unreadable or clipped location text
- No floating controls obscuring critical modal actions
- No hardcoded-feeling repetitive durations across all signals

---

## 20. UI Implementation Notes from v0 Prototype

This section records component-level implementation observations derived from the v0 prototype screenshots. These notes are binding implementation guidance for the React Native MVP.

### 20.1 Home Screen — Component Anatomy

**Header:**
- "Hali" wordmark: large bold dark text, top-left
- Locality selector row: teal location pin icon + locality name in teal + dropdown chevron (↓), tappable
- Locality state subtitle: appears directly below the selector in smaller gray text
  - "Elevated activity" renders in amber/orange
  - "X active signals" renders in teal
  - "Currently calm" renders in muted gray
- "What's the Hali?" prompt: subtle gray text, appears between header and signal sections

**Section labels:**
- "ACTIVE NOW" and "OFFICIAL UPDATES" use small caps, muted gray
- Sections only render if they have content

**Signal cards:**
- White background, large border radius (~12–16dp), subtle drop shadow
- Left: circular teal icon container with white category icon (warning triangle for roads/transport, lightning bolt for electricity, droplet for water/flooding)
- Top-right: teal "● LIVE" indicator
- Title: bold, 2 lines max
- Condition chip: pill shape, amber/orange for negative conditions ("No power", "Difficult to pass", "Unstable power", "Slow moving", "Traffic blocked")
- Participation row: person icon + count + "affected" OR italic gray "Unconfirmed report"
- Duration: clock icon + "Ongoing for ~X"
- Secondary status line:
  - "Being addressed · Updated X min ago" — renders in teal
  - "Reports slowing · Updated X min ago" — renders in muted gray
  - "Unconfirmed report · Updated just now" — renders in italic muted gray

**Official updates on home:**
- Rendered as a compact flat list below active signals
- Each row: institution icon + institution name + brief description text
- Tapping a row opens the official update modal

**FAB:**
- Large teal circle, white + icon, fixed bottom-right
- Approx 56dp diameter

**Bottom navigation:**
- Home / Your Areas / Profile
- Active tab uses teal icon + label
- Inactive tabs use muted gray

### 20.2 Switch Area Modal

Triggered by tapping the locality name in the home header.

- Bottom sheet / modal: "Switch Area" title + X close
- List of followed areas, each showing area name + signal count or "Currently calm"
- Currently selected area has teal text + checkmark on right
- Tapping an area closes modal and updates the home feed

### 20.3 Signal Detail Page — Component Anatomy

**Header bar:**
- Back arrow (←) + "Signal Details" centered title + "● LIVE" badge top-right (teal)

**Hero section:**
- Large circular teal icon container
- Signal title: large bold (2–3 lines)
- Location: location pin icon + location label (gray)
- State chip: outlined pill — "Possible Restoration" in teal outline; "Active" (if shown) in similar style

**Stats row:**
- Person icon + bold count + "affected"
- Clock icon + "Ongoing for ~X"

**Secondary status line:**
- "Being addressed · Updated X min ago" in teal

**Situation Details card:**
- "SITUATION DETAILS" section label (small caps)
- White rounded card:
  - "What people are reporting" label (gray small) + description text (dark)
  - Horizontal divider
  - "Current condition" label + bold value | "Started" label + bold value (side-by-side two-column)
  - Horizontal divider
  - "Location" label + location value

**Official Update section:**
- "OFFICIAL UPDATE" section label (small caps)
- Compact card: initial avatar circle + institution name + "Updated ~X min ago" + excerpt text
- Tapping expands to full modal overlay (see §20.4)

**Restoration Status section:**
- "RESTORATION STATUS" section label (small caps)
- Post-action confirmation text in teal:
  - "Thanks! Your feedback helps confirm restoration" — shown after user has responded
  - "Noted. We'll keep monitoring this signal" — shown when monitoring without action
- Restoration prompt buttons shown when relevant (§7.4)

### 20.4 Official Update Modal

Triggered by tapping the compact official update card on the signal detail page or from the home screen official updates section.

- White bottom sheet overlay with rounded top corners
- Header: institution initial avatar + institution name (bold) + "OFFICIAL UPDATE" label (small caps, muted) + X close button
- Body: full update text, large readable size
- Footer: "Updated ~X min ago" (clock icon) + "Affected: [location]" (pin icon)
- Closes on backdrop tap or X button

### 20.5 NLP Composer Modal — Component Anatomy

Triggered by the FAB (+) button from anywhere in the app.

**Empty state:**
- "What's the Hali?" bold header + X close
- Text input: teal border, placeholder "Describe what's happening… (keep it short and clear)"
- Below input: helper text + "0 / 150" counter aligned right
- "Report" button: full-width teal, slightly muted when input is empty

**Typing state (inference active):**
- Input has teal focus border
- "Be specific: what's happening and where?" helper text below
- Counter: "33 / 150" (updates live)
- Inferred category appears: "Suggested category: Roads / Traffic" in teal
- Inferred condition appears: "· Condition: Potholes" in teal

**Similar signal found state:**
- "This looks similar to" section header (bold, small)
- Similar signal card: amber/orange icon background, signal title, condition chip, count, duration, "Based on nearby reports" footer text
- Action buttons (side by side, full width combined):
  - "I'm affected" — filled teal
  - "I'm observing" — outlined (only shown for observational signal types)
- "Create new signal instead" text link below buttons
- Helper footnote: "Affected = this is directly impacting you · Observing = you can see it happening"

**Keyboard behavior:**
- Modal must remain usable with keyboard open
- Content must scroll or adjust so action buttons are not hidden by keyboard

### 20.6 Your Areas Screen — Component Anatomy

**Header:**
- "Your Areas" bold title
- "Tap to view signals in each area" subtitle in gray

**Area cards:**
- White rounded card
- Left: circular teal-tinted location pin icon container
- Area name (bold) + optional CURRENT badge (small caps, teal, inline)
- Status line:
  - "Elevated activity" — amber/orange
  - "X active signal(s)" — teal
  - "Currently calm" — muted gray
- Timestamp: "Last checked ~X min ago" or "Last checked just now" in small gray
- Right: "✓ Following" pill (teal outlined, with checkmark)

**Footer:**
- "+ Add another area" text link in teal
- "Saved on this device" with phone icon (shown when user is not signed in)

**Add area modal:**
- "Add an area" header + "Select an area to follow" subtitle + X close
- List of available (unfollowed) areas with + button per row
- Each row shows area name + current state ("Currently calm" / "X active signal")
- **The FAB must be hidden or repositioned when this modal is open** to prevent obscuring the area list

### 20.7 Profile Screen — Component Anatomy

**Signed-in state:**
- User card: circular teal person avatar + name (bold) + "Anonymous participation enabled" (gray) + "→ Sign out" link
- "FOLLOWED AREAS" section (small caps label): followed area cards (compact version, same data as Your Areas)
  - "Tap to view signals" subtitle
  - Each row: pin icon + area name + active count + ✓ Following pill

**Signed-out state:**
- User card: gray avatar + "You" + "Not signed in" + "→ Sign in / Create account" button (outlined)
- "FOLLOWED AREAS" section (same structure)
- "SETTINGS" section: "Preferences" row with chevron
- "DEMO CONTROLS" section: "Reset demo data" row with "Clear all signals and start fresh" subtitle
- Footer: "Hali v1.1 — Civic awareness for your locality" in centered small gray text

### 20.8 Color System (Prototype-Validated)

| Token | Usage | Approx hex |
|---|---|---|
| Primary teal | CTAs, active icons, LIVE marker, tappable text, active nav, Following pill | #2D9B8A or similar |
| Amber / orange | Condition chips for negative states, "Elevated activity" | #E8872A or similar |
| Muted gray | Secondary text, calm state, disabled / unconfirmed states | #8A9099 or similar |
| Background | App background | #F3F6F7 or similar |
| Card surface | Signal and detail cards | #FFFFFF |
| Dark text | Titles, primary body text | #1A1F24 or similar |

Exact hex values should be confirmed from the design source files. These are prototype-derived approximations.

### 20.9 Known Prototype Bugs to Fix Before Demo

The following issues were observed in v0 prototype screenshots and must be resolved:

| # | Screen | Issue | Fix Required |
|---|---|---|---|
| 1 | Home (Industrial Area) | Duration displaying "~1663 hr 10 min" — broken timestamp calculation, likely epoch or timezone issue | Fix `occurred_at` / `first_seen_at` timestamp handling; ensure all demo seed data uses correct relative timestamps |
| 2 | Home (Industrial Area) | Duplicate signal cards: "Traffic obstruction at Industrial Area junction" appears twice | Fix clustering/deduplication logic in demo data or signal query |
| 3 | Profile (signed-out) | "South B" appears twice in followed areas list | Fix deduplication in followed areas list query |
| 4 | Home (official update modal) | FAB (+ button) partially obscures the official update modal content | Hide or reposition FAB when any modal or bottom sheet is open |
| 5 | Your Areas (add area modal) | FAB partially obscures the "Add an area" modal | Same fix as above — FAB must not render over open modals |
| 6 | Signal Detail (restoration status) | "Thanks! Your feedback helps confirm restor..." text is truncated | Add bottom padding / safe area inset to restoration status section; ensure text wraps correctly |

---

## 21. MVP Success Criteria (Citizen App)

The citizen app MVP is successful if:

1. A new viewer instantly understands "what's happening"
2. A user feels validated by seeing shared lived experience
3. The app feels alive within 60 seconds
4. New reports appear meaningful and believable
5. Official response is visible but not dominant
6. Resolution feels legible and gradual
7. The product feels like civic infrastructure, not social chatter

---

## 22. Final Instruction for Claude / Builders

When generating or modifying citizen app features, always ask:

**Does this improve clarity, speed, trust, and the civic feedback loop?**

If not, reject it.

When forced to choose between:
- more features
- more polish
- more complexity
- more legibility

Choose:
- legibility
- speed
- trust
- realism of civic signal behavior

---

## 23. Status

This document is now the **canonical citizen mobile app MVP specification** for Hali, incorporating:

- original architecture doctrine
- UX rules
- v0 prototype learnings
- demo simulation requirements
- product language and interaction decisions
- UI implementation notes from v0 screenshots (§20)

Institution dashboard architecture should be specified separately.

---

**END OF FILE**

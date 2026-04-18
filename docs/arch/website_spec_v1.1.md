# Hali Website Spec — v1.1
*Public marketing site · gethali.app (primary) · whatsthehali.com (campaign redirect)*
*Updated: April 2026 · Surface: hali-web · Audience: Citizens (primary), Institutions (secondary)*

---

## 0. Domain and Entry Strategy

### Primary domain
**`https://gethali.app`** — main website (product, conversion, all canonical URLs)

### Campaign domain
**`https://whatsthehali.com`** — social, word-of-mouth, curiosity entry point
- Redirects to `gethali.app` with a temporary redirect initially (307 via Vercel `permanent: false`)
- Switch to 308 (`permanent: true`) only once the campaign URL + destination are fully locked and expected to remain stable
- No content served from this domain directly

### Usage principle
- `gethali.app` = action ("get it, open it, use it")
- `whatsthehali.com` = curiosity ("what is this thing I keep hearing about?")

### Implementation note
The `whatsthehali.com` redirect is a Vercel domain redirect — one config line, no code required. Both domains added to the Vercel project. SSL handled automatically.

---

## 1. Purpose and Scope

The Hali website lives in `apps/hali-web` (created in Phase A) and is a **public marketing and conversion site**. Its job is not to explain everything — it is to trigger a behavior: open or download the app.

> This site is a behavior trigger, not an explanation platform.
> Goal: the visitor thinks "let me check Hali."

### In scope
- Static marketing pages (SSG via Next.js)
- Pre-launch email capture CTA (swaps to App Store / Play Store links when app is live)
- Pilot inquiry form for prospective institution partners
- Brand presence and mission communication

### Out of scope — never build these on this site
- Authentication / login
- Real-time backend API calls or data display
- App functionality embedded in the site
- Dashboard previews that call the live Hali API
- Comments, reactions, or social sharing buttons
- Media uploads
- Admin or ops interface
- Blog or content management in v1

---

## 2. Content Readiness

| Asset | Status | Notes |
|---|---|---|
| Logo / wordmark | Available | Use from brand assets |
| Brand copy | Available | "What's happening in your area" · "What's the Hali?" |
| App screenshots | Available (v0 UI) | Representative, not final — static use only |
| App Store link | Not yet live | Pre-launch email capture until available |
| Play Store link | Not yet live | Pre-launch email capture until available |

**Content rules:**
- Screenshots are representative — do not label them as final UI
- No real-time data displayed anywhere on the site
- Use realistic but fully static examples throughout
- All copy prioritises clarity, trust, and immediacy

---

## 3. Tech Stack

| Concern | Decision | Notes |
|---|---|---|
| Framework | Next.js 14+ (App Router) | SSG for all marketing pages |
| Language | TypeScript 5.4+ | |
| Styling | Tailwind CSS + `@hali/design-system` tokens | Citizen theme only |
| Package manager | pnpm (monorepo) | Already established |
| Build pipeline | Turborepo | Already established |
| Animations | Framer Motion (lightweight) | Load reveals, scroll effects only |
| Deployment | Vercel | Free tier sufficient; both domains configured here |

---

## 4. Design System and Aesthetic Direction

### Token source
Consume `@hali/design-system` — **citizenTheme only**. No hardcoded hex values anywhere in `apps/hali-web`.

### Aesthetic intent: Editorial civic calm
- Warm teal primary (citizen token set)
- Off-white and warm neutral backgrounds
- Generous negative space
- Typography: DM Serif Display (display) + Plus Jakarta Sans (body)
- Subtle grain texture on hero sections
- Animations: restrained — staggered load reveal, scroll-triggered. No looping.

### What the site must never feel like
- A flashy consumer startup or social app
- A SaaS landing page with purple gradients
- A government portal

### Anti-patterns — do not introduce
- Abstract gradient hero backgrounds
- Generic SaaS layouts
- Fake testimonials or "Trusted by" logo strips
- Overuse of decorative icons without contextual meaning
- Marketing buzzwords not present in the spec copy

The site must feel real, grounded, and immediately useful. Nothing invented for the sake of looking like a startup.

---

## 5. Page Inventory

| Route | Page | Priority |
|---|---|---|
| `/` | Home | P0 |
| `/how-it-works` | How It Works | P0 |
| `/for-institutions` | For Institutions | P1 |
| `/about` | About | P1 |
| `/privacy` | Privacy Policy | Required (legal) |
| `/terms` | Terms of Use | Required (legal) |

---

## 6. Navigation

### Desktop
```
[Hali logo]     How It Works   For Institutions   About    [Get notified at launch →]
```
- Sticky, scroll-aware background (transparent → solid at 20px scroll)
- CTA controlled by `NEXT_PUBLIC_APP_LAUNCH_STATE`

### Mobile
- Hamburger drawer, CTA at bottom

### CTA language
- Pre-launch: "Get notified at launch"
- Live: "Open Hali"
- Never use: "Download App"

---

## 7. Home Page Sections

### S1 — Hero
- Headline: `What's happening in your area?`
- Sub-copy: `Know what's affecting your area — roads, water, power — as it happens. Hali brings together what residents are experiencing and what authorities are doing about it.`
- Brand line: `What's the Hali?`
- Primary CTA: `Get notified at launch` (pre-launch) / `Open Hali` (live)
- Secondary CTA: `See how it works →`
- Visual: phone mockup, home feed screenshot

### S2 — Value Proposition
- Title: `A clearer picture of your area`
- Intro: `Hali shows what's happening around you — and how it's being resolved.`
- 3 cards: See what's happening / Add your voice / See who's responding

### S3 — Civic Loop
- Section ID: `how-it-works` (anchor target)
- Title: `How Hali works in real life`
- Intro: `When something happens, you're not the only one seeing it. Hali turns individual reports into a shared, visible reality.`
- 4 steps: Report → Cluster → Response → Resolution

### S4 — Screenshots
- Title: `Built for how your city actually works`
- 3 phone frames: Home feed / Signal detail / Composer

### S5 — Institution Strip
- Headline: `Are you a public institution or utility?`
- Body includes: `No more guessing what's happening on the ground.`
- CTA: `Become a pilot partner →`

### S5.5 — Behavioral Hook
- Title: `Stay ahead of what's happening`
- Body: `Road closures. Water outages. Power issues. Know before it affects you.`
- CTA: `Open Hali`

---

## 8. CTA Strategy

| State | Wording | Behaviour |
|---|---|---|
| Pre-launch | "Get notified at launch" | Email capture modal |
| Live | "Open Hali" | App Store / Play Store links |

Switch: `NEXT_PUBLIC_APP_LAUNCH_STATE=prelaunch` or `live`. No code changes are required to flip this, but for a static/SSG Next.js site a rebuild/redeploy on Vercel is required for the change to take effect.

---

## 9. Environment Variables

```env
NEXT_PUBLIC_APP_LAUNCH_STATE=prelaunch
NEXT_PUBLIC_APP_STORE_URL=
NEXT_PUBLIC_PLAY_STORE_URL=
NEXT_PUBLIC_SITE_URL=https://gethali.app
NOTIFY_EMAIL=hello@gethali.app
INQUIRY_EMAIL=hello@gethali.app
RESEND_API_KEY=
```

---

## 10. SEO

| Page | Title |
|---|---|
| `/` | Hali — What's happening in your area |
| `/how-it-works` | How Hali works — The civic feedback loop |
| `/for-institutions` | For Institutions — Become a Hali pilot partner |
| `/about` | About Hali — Civic infrastructure for real-world cities |

- OG image: 1200×630, `public/og-image.png`
- Canonical domain: `gethali.app`

---

## 11. What This Site Is Not — Hard Gate for Claude Code

If any of the following appear in implementation, stop and flag:
- No `/v1/*` API calls to the Hali backend
- No authentication, sessions, or JWT handling
- No global client state beyond modal open/close
- No WebSockets or real-time subscriptions
- No admin or protected routes
- No media uploads, comments, reactions, or social features

---

## 11.5. Review Handling Gate — Hard Merge Requirement

After a PR is opened for any phase of this site, the PR may not be merged
until **every** review comment (human, Copilot, CodeQL, or GitHub Advanced
Security) has been handled. "Handled" means all of the following for each
review thread:

1. **Evaluated** — classified as one of:
   - `VALID_AND_FIX_NOW` — fix in the same PR
   - `VALID_BUT_DEFER_TO_ISSUE` — open a tracking issue, link it in the reply
   - `NOT_VALID` — reply with the technical reason
2. **Fixed or explicitly dispositioned** — a `VALID_AND_FIX_NOW` item must
   have a commit in the PR that directly addresses it; a deferred item must
   have a linked issue; a `NOT_VALID` item must have a reasoned reply.
3. **Replied to** — every thread must have at least one author reply naming
   the commit SHA (for fixes) or the linked issue (for deferrals) or the
   technical reason (for `NOT_VALID`).
4. **Resolved** — every thread must be marked resolved on GitHub.

**Merge gate:**
- CI green across all required checks
- Zero unresolved review threads on the PR
- No valid findings unaddressed

An admin bypass of the branch protection policy (`--admin` on `gh pr merge`)
does **not** bypass this review-handling gate. It is an orthogonal
requirement: branch protection guards the branch; this gate guards review
hygiene.

Process discipline: if a review comment is posted after merge (Copilot
sometimes finishes its pass late), the same four-step handling must be
performed in a dedicated follow-up PR. Unresolved post-merge review threads
from prior phases block opening the next phase's PR.

---

## 12. Build Phases

| Phase | Scope |
|---|---|
| 0 | Dispatch this spec to `docs/arch/` |
| A | Monorepo setup, Next.js scaffold, design token wiring, Nav, Footer, routing skeleton |
| B | Complete home page: all sections, CtaButton, EmailCaptureModal, /api/notify |
| C | `/how-it-works` full page |
| D | `/for-institutions` with pilot inquiry form |
| E | `/about` + `/privacy` + `/terms` |
| F | Polish, responsive pass, performance audit, SEO |

---

## 13. Definition of Done

- Clear value in < 5 seconds on first visit
- CTA visible above the fold on any viewport without scrolling
- Feels useful before signup
- Mobile-first (375px designed first)
- Lighthouse Performance ≥ 90 on mobile
- No hardcoded hex values — citizen tokens only
- No TypeScript errors
- No console errors in production build

### First impression rule (CRITICAL)
Within 3 seconds, the user must understand:
1. This is about their area
2. It shows real-world conditions
3. It helps them act

If this is not achieved — the design fails regardless of technical correctness.

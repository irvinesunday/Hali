# Mobile Screen Inventory (Phase 1 MVP, API-Aligned v2)

## Purpose

This document locks the minimum React Native screen inventory and navigation flow for Hali Phase 1 MVP.

This is a citizen app only.
Do not add admin or institution dashboard screens in this phase.

The OpenAPI spec is the authority for route naming.
This document’s API dependency sections must match the OpenAPI spec.

---

## Navigation Overview

Recommended top-level structure:

- Boot / Splash
- Auth Stack
- Main App Stack

Main App Stack:
- Home
- Cluster Detail
- Signal Composer Step 1
- Signal Composer Step 2
- Signal Composer Step 3
- Ward Following Settings
- Notification Settings
- Minimal Account Settings

Modal or inline flows:
- Restoration Prompt
- Add Further Context
- OTP Resend
- Location Confirmation

---

## 1. Splash / Boot

API dependencies:
- optional session bootstrap
- optional `POST /v1/auth/refresh` if access token expired and refresh token available

---

## 2. Auth – Phone Entry

API dependencies:
- `POST /v1/auth/otp`

---

## 3. Auth – OTP Verification

API dependencies:
- `POST /v1/auth/verify`

Response expectations:
- access token
- refresh token
- account summary
- device/session info as needed

---

## 4. Home Feed

Purpose:
Primary civic pulse surface.

Sections:
1. Active now
2. Official updates
3. Recurring at this time
4. Other active signals

API dependencies:
- `GET /v1/home`
- `GET /v1/localities/followed`

---

## 5. Cluster Detail

API dependencies:
- `GET /v1/clusters/{id}`
- `POST /v1/clusters/{id}/participation`
- `POST /v1/clusters/{id}/context`
- `POST /v1/clusters/{id}/restoration-response`

---

## 6. Signal Composer – Step 1: Free Text Input

API dependencies:
- `POST /v1/signals/preview`

Rules:
- no image upload in MVP

---

## 7. Signal Composer – Step 2: Confirm Extraction

API dependencies:
- uses preview response from `POST /v1/signals/preview`
- optional geocoding confirmation may remain backend-internal or use a dedicated future endpoint if added explicitly to OpenAPI

Rules:
- do not silently submit raw extraction without confirmation

---

## 8. Signal Composer – Step 3: Join Existing or Create New

API dependencies:
- `POST /v1/signals/submit`

Notes:
- server may internally interpret join-vs-create mode from payload
- do not introduce divergent frontend routes if unified submit endpoint exists

---

## 9. Restoration Prompt

API dependencies:
- `POST /v1/clusters/{id}/restoration-response`

---

## 10. Ward Following Settings

Rules:
- max followed wards = 5

API dependencies:
- `GET /v1/localities/followed`
- `PUT /v1/localities/followed`

---

## 11. Notification Settings

API dependencies:
- `POST /v1/devices/push-token`
- `PUT /v1/users/me/notification-settings`

---

## 12. Minimal Account Settings

API dependencies:
- `GET /v1/users/me`
- `POST /v1/auth/logout`

---

## Required Interaction Flows

### Flow A – Login
Splash → Phone Entry → OTP Verification → Home

### Flow B – Token Renewal
App resume or expired access token → `POST /v1/auth/refresh` → continue current flow

### Flow C – Report New Signal
Home → Composer Step 1 → Step 2 → Step 3 → Cluster Detail or Home

### Flow D – Join Existing Signal
Home → Cluster Detail → tap I’m Affected / I’m Observing

### Flow E – Restoration
Push or Detail Surface → Restoration Prompt → Cluster Detail

### Flow F – Ward Management
Home → Ward Following Settings → Home

---

## Non-Negotiable UX Constraints

- Home is list-led
- Confirmation step is required after extraction
- Join/create decision is explicit through the submission flow
- Participation language must remain:
  - I’m Affected
  - I’m Observing
- “Add Further Context” appears only after “I’m Affected”
- Do not add comments, reactions, chat, or media flows
- Do not add a map-first browsing experience in MVP

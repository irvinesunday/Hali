## Version: 1.0
## Last updated: 2026-04-05

# Session mobile-01a — Auth Stack
# Prerequisite: Backend sessions 00–07 complete. Existing scaffold in apps/citizen-mobile/.

## Context
A mobile app scaffold already exists in apps/citizen-mobile/. This session audits and
corrects the auth stack only. Do NOT touch files outside the auth scope listed below.

## Your task this session
Audit and complete the auth stack: phone entry, OTP verification, token storage,
session bootstrap, and the API client's 401 refresh interceptor.

---

## Universal session rules
Read session_prompts/_universal_rules.md before writing any code.

---

## Files in scope (MAX 4 production files)

1. **apps/citizen-mobile/app/(auth)/phone.tsx** — Phone entry screen
   - Must show +254 prefix (Kenya), call POST /v1/auth/otp
   - Loading, error, success states
   - On success → navigate to OTP verify screen

2. **apps/citizen-mobile/app/(auth)/otp.tsx** — OTP verification screen
   - 6-digit OTP input with auto-focus
   - Calls POST /v1/auth/verify with body: { destination, otp, deviceFingerprintHash, platform, appVersion? }
   - On success: store refreshToken in SecureStore, accessToken in SecureStore
   - Call signIn() from AuthContext
   - Navigate to home feed

3. **apps/citizen-mobile/src/context/AuthContext.tsx** — Auth state management
   - States: unknown → unauthenticated | authenticated
   - Boot: restore tokens from SecureStore
   - signIn: persist tokens + dispatch
   - signOut: call POST /v1/auth/logout, clear SecureStore, dispatch
   - Register auth failure handler with API client

4. **apps/citizen-mobile/src/api/client.ts** — API client with auth interceptor
   - Attach access token to all requests
   - On 401: attempt silent refresh via POST /v1/auth/refresh
   - On refresh success: retry original request once
   - On refresh failure: clear tokens, call auth failure handler
   - Queue concurrent requests during refresh (avoid thundering herd)

## Supporting files (read but do not rewrite unless broken)
- src/api/auth.ts — auth API functions (already exists, verify shapes match)
- src/types/api.ts — type definitions (already exists)
- src/config/constants.ts — SecureStore key names (already exists)
- src/config/env.ts — API_BASE_URL (already exists)

## Spec compliance checks
- RefreshRequestBody must include { refreshToken: string } — verify against backend contract
- VerifyOtpRequestBody must include { destination, otp, deviceFingerprintHash, platform }
- SecureStore keys: HALI_ACCESS_TOKEN, HALI_REFRESH_TOKEN, HALI_ACCOUNT_ID
- Access token attached via Authorization: Bearer header
- No token stored in AsyncStorage — SecureStore only

## Agent A note
Agent A: verify the existing backend compiles and all tests pass. No new backend code.
Produce AGENT_A_CONTRACT with the 4 auth endpoints and their request/response shapes.

## Agent B note
Agent B: write TypeScript tests (Vitest) for the auth service layer.
Tests MUST live at apps/citizen-mobile/__tests__/api/auth.test.ts
Tests MUST import from '../../src/api/auth' — never re-implement the service.
Required test cases:
- requestOtp: success, network error
- verifyOtp: success (tokens returned), invalid OTP (401)
- refreshToken: success (new tokens), expired refresh (401)
- logout: success, best-effort (error swallowed)
- 401 interceptor: triggers refresh then retries original request
- 401 interceptor: refresh failure triggers auth failure handler

## Agent D contract output
```
AGENT_D_FRONTEND_CONTRACT:
Screens: app/(auth)/phone.tsx, app/(auth)/otp.tsx
Services: src/api/auth.ts (requestOtp, verifyOtp, refreshToken, logout)
State: AuthContext (unknown → unauthenticated → authenticated)
Navigation: phone → otp → home | 401+refresh_fail → phone
```

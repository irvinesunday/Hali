# Agent A — Lessons Learned
# Auto-maintained by the multi-agent orchestration system.
# Agent C appends new lessons after every session. Agent A reads this before writing any code.
# Format: each lesson is a numbered entry under the session that produced it.
# DO NOT edit manually — this file is managed by scripts/orchestrate.py.

## How to use this file
- Read every lesson before writing any code for the current session
- Each lesson describes a mistake made in a prior session and the correct pattern
- Lessons are permanent — they apply to all future sessions, not just the one they came from
- If you are about to do something that matches a MISTAKE pattern, stop and use the CORRECT pattern instead

---

## Session 00 — Sync (baseline, no lessons yet)
No sessions completed yet. This file will grow as sessions run.

---

## Session mobile-01 — citizen-mobile-app (2026-04-05)

AGENT_C_LESSONS:
Session: mobile-01
Phase: Phase 1 MVP — Citizen Mobile App

LESSON 1:
Category: Coverage
Mistake: Agent B re-implemented the service under test inside the test file
  instead of importing the real production module.
Correct: Tests must import the real module. A test that re-implements the
  subject provides zero coverage of production bugs. The test file should
  begin with `import { requestOtp, verifyOtp } from '../lib/auth'` — never
  with a local reimplementation of those functions.
Example:
  WRONG:  // Inside auth.test.ts
          async function requestOtp(payload) {
            const res = await fetch(...);
            ...
          }
          it('calls the right endpoint', async () => { await requestOtp(...) });

  RIGHT:  import { requestOtp } from '../lib/auth';
          it('calls the right endpoint', async () => { await requestOtp(...) });

LESSON 2:
Category: Contracts
Mistake: Agent B used field name "code" for the OTP value in VerifyOtpPayload,
  but the AGENT_A_CONTRACT specifies the field as "otp". The contract also
  requires "deviceFingerprintHash" and "platform" fields which were omitted.
Correct: Before writing any DTO or payload shape in tests, read the
  AGENT_A_CONTRACT for that endpoint verbatim. The field names in the test
  doubles must match the contract exactly — a renamed field means the mock
  validates the wrong shape and real integration bugs are invisible.
Example:
  WRONG:  interface VerifyOtpPayload { destination: string; code: string; deviceId: string; }
  RIGHT:  interface VerifyOtpPayload {
            destination: string;
            otp: string;
            deviceId: string;
            deviceFingerprintHash: string;
            platform: string;
            appVersion?: string;
          }

LESSON 3:
Category: Contracts
Mistake: Agent B's refreshTokens() test double sent only { refreshToken }
  to POST /v1/auth/refresh, omitting the required deviceId field.
Correct: POST /v1/auth/refresh body must include { refreshToken: string,
  deviceId: string }. Every request body shape must be verified against the
  AGENT_A_CONTRACT before writing tests or service code.
Example:
  WRONG:  body: JSON.stringify({ refreshToken: stored })
  RIGHT:  body: JSON.stringify({ refreshToken: stored, deviceId: currentDeviceId })

LESSON 4:
Category: Coverage
Mistake: Agent B wrote no tests for the critical 401 → silent refresh →
  retry-once flow, and no tests for the proactive 5-minute expiry refresh
  on app resume. These are the two highest-risk mobile auth behaviours and
  the most common source of production auth bugs.
Correct: Both flows require explicit tests:
  (a) A test that mocks the first API call returning 401, then asserts that
      refreshTokens() was called, then asserts the original request was
      retried exactly once, then asserts the retried call succeeds.
  (b) A test that sets accessTokenExpiresAt to (now + 4 minutes), calls
      getValidToken(), and asserts a refresh was triggered before returning
      the token.
Example:
  RIGHT (401 retry):
    mockFetch
      .mockResolvedValueOnce(makeFetchResponse({ code: 'unauthorized' }, 401))
      .mockResolvedValueOnce(makeFetchResponse({ accessToken: 'new', ... }, 200)) // refresh
      .mockResolvedValueOnce(makeFetchResponse({ data: 'ok' }, 200)); // retry
    const result = await authenticatedGet('/v1/home');
    expect(mockFetch).toHaveBeenCalledTimes(3);
    expect(result.data).toBe('ok');

LESSON 5:
Category: Coverage
Mistake: Agent B tested that a SecureStore constant was defined but never
  asserted that SecureStore.setItemAsync was called with the key
  "hali_refresh_token". A key rename would silently break the auth contract
  without failing any test.
Correct: Assert the exact key in storage tests.
Example:
  WRONG:  expect(SecureStore.setItemAsync).toHaveBeenCalled();
  RIGHT:  expect(SecureStore.setItemAsync).toHaveBeenCalledWith(
            'hali_refresh_token',
            expect.any(String)
          );

LESSON 6:
Category: Architecture
Mistake: Agent B placed TypeScript test files under
  tests/Hali.UnitTests/mobile/ — a .NET test project path that is picked
  up by `dotnet test`, not Vitest. TypeScript tests placed here will never
  run in the mobile CI pipeline.
Correct: Mobile app tests must live inside the mobile app directory tree,
  co-located with the source they test or in a dedicated __tests__ folder:
    apps/citizen-mobile/__tests__/lib/auth.test.ts
  or
    apps/citizen-mobile/lib/__tests__/auth.test.ts
  The test runner (Vitest or Jest) is configured in apps/citizen-mobile/
  package.json and will only discover files within that directory.
Example:
  WRONG:  tests/Hali.UnitTests/mobile/lib/auth.test.ts
  RIGHT:  apps/citizen-mobile/__tests__/lib/auth.test.ts

LESSON 7:
Category: Coverage
Mistake: Agent B produced tests for only one of seven required service
  modules (auth.ts only). The session prompt explicitly states "all service
  functions have at least one test for success and one for error path" across
  lib/auth.ts, lib/signals.ts, lib/clusters.ts, lib/localities.ts,
  lib/users.ts, lib/devices.ts, and lib/offline.ts.
Correct: Before declaring test work complete, enumerate every exported
  function from every required module and verify each has a success-path
  and error-path test. Missing modules are a blocking gap, not a warning.
Example:
  Minimum required tests per module (success + error at minimum):
    auth.ts     → requestOtp, verifyOtp, refreshTokens, logout
    signals.ts  → previewSignal (success, nlp_unavailable), submitSignal
    clusters.ts → getCluster, participate, addContext, submitRestoration
    localities.ts → getFollowed, setFollowed (success, max_followed_exceeded)
    users.ts    → getMe, updateNotificationSettings
    devices.ts  → registerPushToken
    offline.ts  → enqueue, flush (success, idempotency key reuse on retry)

<!-- LESSONS_APPEND_MARKER — do not remove this line, orchestrator appends below it -->

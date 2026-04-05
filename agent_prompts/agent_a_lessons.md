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


## Session mobile-01a — mobile-auth (2026-04-05)

AGENT_C_LESSONS:
Session: mobile-01a
Phase: Mobile — Auth Stack

LESSON 1:
Category: Contracts
Mistake: Agent A produced the 4 production files but did not emit the AGENT_A_CONTRACT
  block that the session prompt explicitly requires.
Correct: Every session prompt that contains "Agent A note: produce AGENT_A_CONTRACT"
  requires a structured output block at the end of Agent A's response, listing every
  endpoint, its method+path, and the exact request/response shapes. This block is
  mandatory for Agent B and Agent C to validate alignment.
Example:
  WRONG:  (no AGENT_A_CONTRACT block in output)
  RIGHT:
    AGENT_A_CONTRACT:
      POST /v1/auth/otp     { destination, authMethod, deviceId } → { message, expiresInSeconds }
      POST /v1/auth/verify  { destination, otp, deviceFingerprintHash, platform } → { accessToken, refreshToken, accountId, expiresIn }
      POST /v1/auth/refresh { refreshToken } → { accessToken, refreshToken, expiresIn }
      POST /v1/auth/logout  { refreshToken } → 204

LESSON 2:
Category: Contracts
Mistake: Agent A added `deviceId: string` to RefreshTokenBody and LogoutBody in
  types/api.ts, which contradicts the session spec that specifies only
  `{ refreshToken: string }` for the refresh body.
Correct: When the session spec explicitly states the request body shape, use exactly
  those fields. Do not add extra fields without (a) evidence from the backend contract
  and (b) a comment citing that evidence. Silent field additions break Agent B's tests
  and the backend contract.
Example:
  WRONG:  export interface RefreshTokenBody { refreshToken: string; deviceId: string; }
  RIGHT:  export interface RefreshTokenBody { refreshToken: string; }
  // If deviceId IS required by the backend, cite the source:
  // Per backend contract session-07, POST /v1/auth/refresh requires deviceId for token rotation.

LESSON 3:
Category: Contracts
Mistake: Agent A added an undocumented SecureStore key (HALI_DEVICE_ID) that is not
  listed in the session spec ("SecureStore keys: HALI_ACCESS_TOKEN, HALI_REFRESH_TOKEN,
  HALI_ACCOUNT_ID").
Correct: Only define SecureStore keys that are listed in the session spec. If a new key
  is needed, surface the need as an explicit assumption before writing code.
Example:
  WRONG:  DEVICE_ID: 'HALI_DEVICE_ID',  // not in spec
  RIGHT:  // Only the three keys listed in the session spec:
          ACCESS_TOKEN: 'HALI_ACCESS_TOKEN',
          REFRESH_TOKEN: 'HALI_REFRESH_TOKEN',
          ACCOUNT_ID: 'HALI_ACCOUNT_ID',

LESSON 4:
Category: Other
Mistake: Agent A rewrote supporting files (constants.ts, env.ts, types/api.ts) that the
  session prompt explicitly marks as "read but do not rewrite unless broken", without
  explaining why they were broken.
Correct: When the session prompt restricts files to read-only, treat them as read-only
  unless you explicitly state "File X was broken because <reason>" before rewriting it.
  Silent rewrites of out-of-scope files violate the MAX-4-production-files constraint.
Example:
  WRONG:  (rewrite constants.ts without comment)
  RIGHT:  // AUDIT: constants.ts was missing HALI_ACCOUNT_ID key required by spec.
          //        Rewriting under the "unless broken" exception.
          //        This counts against the 4-file budget.

LESSON 5:
Category: Coverage
Mistake: Agent A's auth.ts output was truncated — the full implementation of requestOtp
  and all subsequent functions was cut off. Agent C cannot validate error handling,
  network error path, or whether the file compiles.
Correct: Each production file must be complete in Agent A's output. If the response
  token limit is a concern, produce fewer files per session rather than truncating any
  file. An incomplete file is worse than no file — it creates an illusion of coverage.
Example:
  WRONG:  export async function requestOtp(...): Promise<...> {
            const response =   ← truncated
  RIGHT:  Produce the complete function including try/catch, error mapping, and return.
          If token budget is tight, split into two sessions.


## Session mobile-01a — mobile-auth (2026-04-05)

AGENT_C_LESSONS:
Session: mobile-01a (second attempt)
Phase: mobile-01a — Auth Stack

LESSON 1:
Category: Other
Mistake: Agent A produced a truncated phone.tsx for the THIRD consecutive time
  despite LESSON 5 from the prior session explicitly prohibiting truncation.
Correct: Every file submitted must be complete from the first line to the
  closing brace. If a response would be cut off by context length, split the
  output into clearly labelled parts (Part 1/2, Part 2/2) rather than
  submitting a partial file. A truncated file is worse than no file.
Example:
  WRONG:  function toE164(input: string): string {
            const digits = input.replace(/\D/g, '');
            if (digits.startsWith('0') && digits.length === 10) {
              return '+254' + digits.slice(1);
            }
            // ... file ends here
  RIGHT:  /* If context is tight, emit: */
          // [PART 1 of 2 — phone.tsx lines 1-80]
          // ... complete block ...
          // [Continued in Part 2]

LESSON 2:
Category: Architecture
Mistake: The token refresh call inside `attemptTokenRefresh` used raw `fetch()`
  instead of routing through the shared `executeRequest` abstraction.
Correct: All HTTP calls — including the refresh endpoint — must go through
  `executeRequest` so that error normalisation, timeout handling, and any
  future middleware apply uniformly.
Example:
  WRONG:  const response = await fetch(`${baseUrl}/v1/auth/refresh`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken }),
          });
  RIGHT:  const result = await executeRequest<RefreshTokenResponse>(
            baseUrl, '/v1/auth/refresh', 'POST',
            { refreshToken }, { 'Content-Type': 'application/json' }
          );

LESSON 3:
Category: Other
Mistake: `Buffer.from(parts.join('|')).toString('base64')` was used in
  otp.tsx to base64-encode the device fingerprint. `Buffer` is a Node.js
  global not available in React Native / Hermes without an explicit polyfill.
Correct: Use `btoa(parts.join('|'))` (available in Hermes) or declare and
  install a Buffer polyfill (e.g. `buffer` package) and import it explicitly.
  Never rely on implicit Node.js globals in React Native code.
Example:
  WRONG:  return Buffer.from(parts.join('|')).toString('base64');
  RIGHT:  return btoa(unescape(encodeURIComponent(parts.join('|'))));
          // or: import { Buffer } from 'buffer'; (with polyfill installed)

LESSON 4:
Category: Contracts
Mistake: Agent A exported auth functions as `refreshTokenApi` and `logoutApi`
  but the session prompt and Agent B's tests expected `refreshToken` and
  `logout`. The name mismatch caused all Agent B mock intercepts to fail.
Correct: Export names in auth.ts must exactly match what the session prompt
  specifies and what Agent B is told to import. When in doubt, use the
  simplest unambiguous name the prompt prescribes.
Example:
  WRONG:  export async function refreshTokenApi(...) { ... }
          export async function logoutApi(...) { ... }
  RIGHT:  export async function refreshToken(...) { ... }
          export async function logout(...) { ... }

LESSON 5:
Category: Contracts
Mistake: `ApiError` and `Result<T,E>` were declared in two separate files
  (`client.ts` and `src/types/api.ts`), creating duplicate type definitions
  that diverge over time.
Correct: Declare shared types in exactly one canonical location
  (`src/types/api.ts`) and import from there everywhere else — including
  inside `client.ts`.
Example:
  WRONG:  // in client.ts
          export interface ApiError { status: number; code: string; ... }
          // in src/types/api.ts
          export interface ApiError { status: number; code: string; ... }
  RIGHT:  // in src/types/api.ts only
          export interface ApiError { status: number; code: string; message: string; }
          // in client.ts
          import type { ApiError, Result } from '../types/api';

LESSON 6:
Category: Architecture
Mistake: In `attemptTokenRefresh`, `isRefreshing = false` was set BEFORE
  calling `flushQueue(null, err)` in the no-refresh-token error path. A
  concurrent caller arriving between those two lines would start a second
  refresh cycle, bypassing the queue.
Correct: Always set `isRefreshing = false` AFTER `flushQueue` completes so
  the flag transition and queue drain are atomic from the perspective of any
  concurrent caller.
Example:
  WRONG:  isRefreshing = false;   // ← too early
          flushQueue(null, err);
          throw err;
  RIGHT:  flushQueue(null, err);
          isRefreshing = false;   // ← after flush
          throw err;

LESSON 7:
Category: Contracts
Mistake: SecureStore constants were exported as properties of a `SECURE_STORE_KEYS`
  object but the session prompt implied — and Agent B assumed — they were
  standalone named exports (e.g. `import { HALI_ACCESS_TOKEN } from constants`).
Correct: Either (a) export as standalone named constants to match the session
  prompt's implied usage, or (b) explicitly document the object-property shape
  in the AGENT_A_CONTRACT block so Agent B knows the correct import path.
  Never leave the export shape ambiguous between agents.
Example:
  WRONG:  export const SECURE_STORE_KEYS = {
            ACCESS_TOKEN: 'HALI_ACCESS_TOKEN',
            ...
          };
          // Agent B imports: import { HALI_ACCESS_TOKEN } from constants  → undefined

  RIGHT (option a — standalone):
          export const HALI_ACCESS_TOKEN = 'HALI_ACCESS_TOKEN';
          export const HALI_REFRESH_TOKEN = 'HALI_REFRESH_TOKEN';
          export const HALI_ACCOUNT_ID   = 'HALI_ACCOUNT_ID';

  RIGHT (option b — object, explicitly documented):
          export const SECURE_STORE_KEYS = { ... } as const;
          // AGENT_A_CONTRACT must state: "import SECURE_STORE_KEYS.ACCESS_TOKEN,
          //   not a standalone HALI_ACCESS_TOKEN export"


## Session integration-tests — integration-tests (2026-04-05)

AGENT_C_LESSONS:
Session: 08
Phase: Integration Tests

LESSON 1:
Category: Architecture
Mistake: Agent A defined HaliWebApplicationFactory in one namespace while Agent B
  independently defined a second copy of the same class in a different namespace.
  Both files would be compiled into the same test assembly, causing a name conflict
  and a build failure.
Correct: In test infrastructure sessions, Agent A owns all shared test infrastructure
  classes (factories, base classes, fakes). Agent B must import from Agent A's namespace
  and must never redefine a class that Agent A already provides. If Agent B needs a
  variant, it must subclass or compose — never redeclare.
Example:
  WRONG:  // Agent B creates Infrastructure/HaliWebApplicationFactory.cs with its own
          // class HaliWebApplicationFactory : WebApplicationFactory<Program>
  RIGHT:  // Agent B adds a using directive:
          // using Hali.Tests.Integration; // Agent A's factory
          // and references Factory.CreateClient() from IntegrationTestBase

LESSON 2:
Category: Architecture
Mistake: Agent A's output was truncated mid-method, leaving ConfigureWebHost incomplete.
  The critical service replacements (ISmsProvider, IGeocodingService, IPushNotificationService)
  were not shown or confirmed as present. External services that send real network requests
  (SMS, geocoding, push) were not demonstrably stubbed.
Correct: In integration test factories, ALL external side-effect services must be replaced
  with fakes in ConfigureWebHost. The replacement list must be explicit and complete:
  ISmsProvider → FakeSmsProvider, INlpExtractionService → FakeNlpExtractionService,
  IGeocodingService → FakeGeocodingService, IPushNotificationService → FakePushNotificationService.
  Each fake must be shown as a concrete file, not implied.
Example:
  WRONG:  // (service replacement implied but not shown; file truncated)
  RIGHT:  services.RemoveAll<ISmsProvider>();
          services.AddScoped<ISmsProvider, FakeSmsProvider>();
          services.RemoveAll<IGeocodingService>();
          services.AddScoped<IGeocodingService, FakeGeocodingService>();
          services.RemoveAll<IPushNotificationService>();
          services.AddScoped<IPushNotificationService, FakePushNotificationService>();
          services.RemoveAll<INlpExtractionService>();
          services.AddScoped<INlpExtractionService, FakeNlpExtractionService>();

LESSON 3:
Category: Contracts
Mistake: Agent A's FakeNlpExtractionService file was truncated mid-property-assignment.
  A fake that returns an incomplete NlpCandidateDto (missing required fields) will cause
  the application's taxonomy validation to reject it with 422, making all preview and
  submit tests fail for the wrong reason.
Correct: Every fake service must return a fully valid response that passes all downstream
  validation gates. For NLP, this means the returned NlpCandidateDto must have all required
  fields (category from the canonical taxonomy, summary, condition, confidence, lat, lng)
  populated with values that will pass the application's validation logic.
Example:
  WRONG:  new NlpCandidateDto { Category = "roads", Summary = "...", Confidence = 0.92,
          // file truncated — remaining fields missing
  RIGHT:  new NlpCandidateDto
          {
              Category   = "roads",
              Summary    = "Pothole on test road",
              Condition  = "pothole",
              Confidence = 0.92,
              Latitude   = -1.286389,
              Longitude  = 36.817223,
              // all required fields present
          }

LESSON 4:
Category: Architecture
Mistake: Agent B removed INlpExtractionService from the DI container using a fragile
  FullName.Contains("INlpExtractionService") string match instead of a type-safe
  typeof() comparison.
Correct: Always use typeof() when removing or replacing service descriptors in
  ConfigureWebHost. String matching silently fails when namespaces change.
Example:
  WRONG:  var nlpDescriptor = services.SingleOrDefault(
              d => d.ServiceType.FullName != null &&
                   d.ServiceType.FullName.Contains("INlpExtractionService"));
  RIGHT:  services.RemoveAll<INlpExtractionService>();
          // or, if RemoveAll is not available:
          var descriptor = services.SingleOrDefault(
              d => d.ServiceType == typeof(INlpExtractionService));
          if (descriptor != null) services.Remove(descriptor);

LESSON 5:
Category: Coverage
Mistake: The test GetCluster_AfterSignalSubmit_ReturnsCluster does not account for
  whether cluster creation is synchronous or asynchronous. If clustering runs in a
  background worker, the test will query for the cluster before it exists, returning
  404 and appearing as a flaky failure.
Correct: Integration tests must not have hidden timing dependencies. Either disable
  background workers in the test host and run clustering inline, or add an explicit
  polling loop with a timeout. Document the chosen approach in a comment.
Example:
  WRONG:  await client.PostAsync("/v1/signals/submit", content);
          var response = await client.GetAsync($"/v1/clusters/{clusterId}");
          response.EnsureSuccessStatusCode(); // may 404 if worker hasn't run yet
  RIGHT:  // Option A — disable worker, invoke clustering synchronously in test helper
          await client.PostAsync("/v1/signals/submit", content);
          await _factory.TriggerClusteringAsync(); // runs inline in test host
          var response = await client.GetAsync($"/v1/clusters/{clusterId}");
          // Option B — poll with timeout
          ClusterDetailResponse? cluster = null;
          var deadline = DateTime.UtcNow.AddSeconds(5);
          while (DateTime.UtcNow < deadline)
          {
              var r = await client.GetAsync($"/v1/clusters/{clusterId}");
              if (r.IsSuccessStatusCode) { cluster = await r.Content.ReadFromJsonAsync<ClusterDetailResponse>(); break; }
              await Task.Delay(200);
          }
          Assert.NotNull(cluster);

LESSON 6:
Category: Contracts
Mistake: The namespace for INlpExtractionService differed between Agent A
  (Hali.Application.Abstractions.Nlp) and Agent B (Hali.Application.Contracts).
  This means one agent's fake would fail to compile against the real interface.
Correct: Before writing any fake or stub in a test project, confirm the exact namespace
  of the interface being faked by reading the Session 05 contract output. Never guess
  a namespace — always derive it from the authoritative source.
Example:
  WRONG:  // Agent B guesses:
          using Hali.Application.Contracts;
          public class FakeNlpExtractionService : INlpExtractionService ...
  RIGHT:  // Read Session 05 AGENT_A_CONTRACT, confirm namespace, then:
          using Hali.Application.Abstractions.Nlp; // confirmed from contract
          public class FakeNlpExtractionService : INlpExtractionService ...

LESSON 7:
Category: Coverage
Mistake: The business rule "one active participation type per device per cluster"
  was tested only by checking HTTP status codes (202). A test that checks only the
  status code of the second request cannot confirm that the first participation was
  actually cancelled — the system might have two active rows and still return 202.
Correct: For business rules that assert a single-active-row invariant, the test must
  also verify the final state — either by re-fetching the cluster detail and inspecting
  the participation field, or by querying the DB directly via the test factory's scope.
Example:
  WRONG:  var r1 = await client.PostAsync("/v1/clusters/{id}/participation", affected);
          var r2 = await client.PostAsync("/v1/clusters/{id}/participation", observing);
          Assert.Equal(HttpStatusCode.Accepted, r2.StatusCode); // only status checked
  RIGHT:  var r1 = await client.PostAsync("/v1/clusters/{id}/participation", affected);
          var r2 = await client.PostAsync("/v1/clusters/{id}/participation", observing);
          Assert.Equal(HttpStatusCode.Accepted, r2.StatusCode);
          // also verify only one active participation exists:
          var cluster = await client.GetFromJsonAsync<ClusterDetailDto>($"/v1/clusters/{id}");
          Assert.Equal("observing", cluster!.MyParticipation); // not "affected"
          // OR query DB directly:
          using var scope = _factory.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<HaliDbContext>();
          var count = await db.Participations.CountAsync(p =>
              p.ClusterId == clusterId && p.DeviceId == deviceId && p.CancelledAt == null);
          Assert.Equal(1, count);

<!-- LESSONS_APPEND_MARKER — do not remove this line, orchestrator appends below it -->

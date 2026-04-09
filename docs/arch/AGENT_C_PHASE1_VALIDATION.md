# Agent C — Phase 1 Re-Validation Report

```
AGENT_C_VALIDATION:
Phase: Phase 1 Complete — Backend Sessions 00-07 + Mobile Phases A-G
Re-validation: YES (previous validation had 1 open BLOCKING item)
Date: 2026-04-09
Overall verdict: PASS_WITH_NOTES

Test results:
  Backend unit tests:        175 passed / 175 total
  Backend integration tests:  15 passed /  15 total
  Mobile Jest tests:         174 passed / 174 total
  Build:                     PASS (0 errors, 17 warnings — pre-existing nullability)

Previously blocking — now resolved:
  BLOCKING-1 (integration tests): RESOLVED — 15 tests in tests/Hali.Tests.Integration
    (Real WebApplicationFactory + Testcontainers PostgreSQL/Redis, executes against
    Docker on local dev. All 15 pass in 7s.)

Agent A (backend) issues:
  BLOCKING:
    - None
  WARNING:
    - src/Hali.Application/Clusters/CivisCalculator.cs:9 — MACF formula is
      `BaseFloor + Log2(sds + 1)` only. mvp_locked_decisions.md §9 specifies
      `BaseFloor + Alpha*Log2(1+SDS) + SensitivityUplift + geoUncertainty*0.5`.
      Alpha=1.0 (harmless absent), but SensitivityUplift (=1 for safety) and
      geoUncertainty term are NOT applied. Safety-category clusters under-floor.
    - src/Hali.Application/Clusters/ClusteringService.cs:122 — join score is
      `0.4 + 0.25*dist + 0.20*time + 0.15*condition`. The 0.40 weight is a
      constant addend, not `0.40 * categoryScore`. Implementation relies on an
      implicit upstream same-category filter; that filter is not visible in the
      ComputeJoinScore method itself. Acceptable in practice but should be
      either documented at the call site or made explicit in the formula.
    - tests/Hali.Tests.Integration/Signals/SignalIntegrationTests.cs — 9 lines
      with whitespace formatting violations. `dotnet format --verify-no-changes`
      fails on this file. Not caught because the integration test project is not
      in the Release solution config (MSB4121 warning).
    - 02_openapi.yaml — `category` fields use `{ type: string }` with no enum
      constraint. The civic_category enum has 8 values and is enforced in DB +
      .env CIVIS keys, but contract clients have no schema-level enforcement.
  MISSING:
    - None

Agent D (mobile) issues:
  BLOCKING:
    - None
  WARNING:
    - apps/citizen-mobile/app/index.tsx — 4 hardcoded hex colors
      (#1a3a2f, #fff x2, #86efac) in splash screen. Violates Phase 1 gate
      "Zero hardcoded hex in mobile screens".
    - apps/citizen-mobile/src/components/common/Empty.tsx — 2 hardcoded hex
      colors (#6b7280, #9ca3af).
    - apps/citizen-mobile/src/components/common/TextInput.tsx — 7 hardcoded
      hex colors (placeholder, label, border, text, background).
    Total: 13 hex occurrences across 3 files. Should be migrated to design
    tokens. None of these are in the 11 required screens — they are shared
    primitives — but they are imported by screens, so they leak hex into
    rendered output.

Schema / contract issues:
  - civic_category enum across sources:
      Schema DDL (01_postgres_schema.sql): roads, water, electricity, transport,
        safety, environment, governance, infrastructure ✓ (8)
      .env.example CIVIS_* keys: ROADS, TRANSPORT, ELECTRICITY, WATER,
        ENVIRONMENT, SAFETY, GOVERNANCE, INFRASTRUCTURE ✓ (8)
      OpenAPI: category typed as plain string — NO enum constraint (see WARNING above)
  - All controller routes (`v1/auth`, `v1/clusters`, `v1/signals`, `v1/home`,
    `v1/localities`, `v1/devices`, `v1/users`, `v1/feedback`, `v1/official-posts`,
    `v1/admin`) align with OpenAPI paths.
  - Mobile API client calls 11 endpoints, all present in OpenAPI. Cluster
    sub-routes (participation, context, restoration-response) constructed
    dynamically in src/api/clusters.ts also match.
  - RefreshToken stored as `token_hash` (HasMaxLength 128) — never plaintext. ✓

Spec drift:
  - mvp_locked_decisions.md §9 vs CivisCalculator.cs — see MACF WARNING above.
  - Phase 1 gate "zero hex" violated by 3 shared component files.

Phase 1 gate criteria:
  [x] All backend unit tests pass (175/175)
  [x] All integration tests pass (15/15, target was 15+)
  [x] All mobile Jest tests pass (174/174)
  [ ] Zero hardcoded hex in mobile screens — 13 occurrences across 3 files
  [x] Zero Ionicons / @expo/vector-icons in mobile screens
  [x] Zero TypeScript errors (mobile) — `tsc --noEmit` clean
  [~] All civic_category enum values consistent across 4 sources — DB and
      env match; OpenAPI does not constrain via enum
  [x] All required screens exist on disk (11/11)
  [x] Outbox pattern present in all cluster state mutations (ClusteringService,
      CivisEvaluationService, ParticipationService, OfficialPostsService all
      reference OutboxEvent)
  [ ] CIVIS MACF formula matches mvp_locked_decisions.md §9 — partial (missing
      SensitivityUplift and geoUncertainty terms)
  [x] No features outside Phase 1 MVP scope

Approved for Phase 1 completion: NO — 2 unchecked criteria above must be
addressed before declaring Phase 1 complete. Both are small, well-bounded
fixes; verdict is PASS_WITH_NOTES (not FAIL) because all tests pass and
no blocking architectural issues remain.
```

```
AGENT_C_LESSONS:
Phase: Phase 1 Re-Validation
Date: 2026-04-09

LESSON 1:
Category: CIVIS
Mistake: MACF calculator drops the SensitivityUplift and geoUncertainty terms
  from the locked formula, so safety-category clusters and high-uncertainty
  geocodes activate at the wrong floor.
Correct: Implement the full §9 formula
  `Clamp(Ceil(BaseFloor + Alpha*Log2(1+SDS) + SensitivityUplift + 0.5*geoUncertainty), MacfMin, MacfMax)`
  with Alpha=1.0, SensitivityUplift=1 for safety/0 elsewhere, geoUncertainty
  passed in from the cluster's worst-case location confidence.
Example:
  WRONG:  double a = (double)opts.BaseFloor + Math.Log2(sds + 1.0);
          int value = (int)Math.Ceiling(a);
  RIGHT:  double raw = opts.BaseFloor
              + opts.Alpha * Math.Log2(1.0 + sds)
              + opts.SensitivityUplift
              + 0.5 * geoUncertainty;
          int value = Math.Clamp((int)Math.Ceiling(raw), opts.MacfMin, opts.MacfMax);

LESSON 2:
Category: Mobile
Mistake: Shared primitive components (TextInput, Empty, splash) hardcode hex
  colors instead of consuming design tokens, leaking raw hex into every screen
  that imports them.
Correct: All color values in apps/citizen-mobile must come from the token
  layer (theme/colors). Primitives are not exempt — they are the largest leak
  surface because every screen consumes them.
Example:
  WRONG:  color: '#6b7280'
  RIGHT:  color: theme.colors.textMuted

LESSON 3:
Category: Coverage
Mistake: tests/Hali.Tests.Integration project is not declared in the Release
  solution configuration, so `dotnet build --configuration Release` skips it
  silently and `dotnet format` finds whitespace violations only on demand.
Correct: Add Hali.Tests.Integration to the Release|Any CPU section of
  Hali.sln so CI catches formatting and build failures in the same pass as
  unit tests.
Example:
  WRONG:  Hali.sln only lists Hali.Tests.Integration under Debug|Any CPU
  RIGHT:  Both Debug|Any CPU and Release|Any CPU configured for the project

LESSON 4:
Category: Contracts
Mistake: OpenAPI `category` properties are typed as `{ type: string }` with
  no `enum:` constraint, so generated clients accept any string and the
  civic_category enum is enforced only at the database layer.
Correct: Define a reusable `CivicCategory` schema component with the 8
  enum values and reference it from every category property in 02_openapi.yaml.
Example:
  WRONG:  category: { type: string }
  RIGHT:  category: { $ref: '#/components/schemas/CivicCategory' }
          # plus components/schemas/CivicCategory: { type: string,
          #   enum: [roads, transport, electricity, water, environment,
          #          safety, governance, infrastructure] }
```

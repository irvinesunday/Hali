# Hali — Testing Strategy
**Test structure, coverage requirements, and patterns for every module.**

---

## Test pyramid

```
Unit tests      → domain rules, pure functions, CIVIS math, lifecycle guards
Integration     → DB + service wiring, queue flows, full endpoint happy paths
Contract tests  → OpenAPI schema compliance, enum stability
E2E tests       → critical user journeys (Phase 1: 3 journeys)
```

---

## Unit test requirements

Every domain rule must have a unit test. No exceptions. The following are mandatory:

### CIVIS engine tests

```csharp
// CivisEngineTests.cs

[Theory]
[InlineData("roads",          18, 2, 6)]
[InlineData("transport",       8, 2, 5)]
[InlineData("electricity",    12, 2, 6)]
[InlineData("water",          24, 2, 7)]
[InlineData("environment",    36, 2, 6)]
[InlineData("safety",         18, 2, 6)]
[InlineData("infrastructure", 24, 2, 6)]
[InlineData("governance",     24, 2, 6)]
public void MACF_StaysWithinBounds_ForEachCategory(
    string category, int halfLifeHours, int macfMin, int macfMax)
{
    // Test at SDS=0, SDS=1, SDS=3, SDS=10
    // MACF must always be clamped within [macfMin, macfMax]
}

[Fact]
public void DecayKernel_HalvesWeight_AtHalfLifeHours()
{
    // K_time at t=0 should be 1.0
    // K_time at t=halfLifeHours should be ~0.5
    // K_time at t=2*halfLifeHours should be ~0.25
}

[Fact]
public void Activation_Fails_WhenUniqueDevicesLessThan2()
{
    // One device, many participations → diversity gate blocks activation
}

[Fact]
public void Activation_Fails_WhenActiveMassLessThanMACF()
{
    // Active mass = 1.5, MACF = 2 → gate not met
}

[Fact]
public void Activation_Passes_WhenAllGatesMet()
{
    // 2+ devices, active mass >= MACF, SDS >= 1.0 → activation passes
}

[Fact]
public void JoinScore_ReturnsCorrectWeights()
{
    // categoryScore=1, distance=ring0 (1.0), time=same (1.0), condition=1.0
    // Expected: 0.40 + 0.25 + 0.20 + 0.15 = 1.0
}

[Fact]
public void JoinScore_BelowThreshold_WhenCategoryMismatch()
{
    // categoryScore=0 → max score = 0 + 0.25 + 0.20 + 0.15 = 0.60 < threshold 0.65
}

[Fact]
public void Restoration_Confirms_AtSixtyPercentWithTwoDevices()
{
    // 3 yes, 2 no, 3 distinct devices → ratio=0.6, confirmed
}

[Fact]
public void Restoration_Rejects_WhenMajorityStillAffected()
{
    // 1 yes, 4 no → rejected
}

[Fact]
public void Restoration_TheftDetection_RevokesAllTokens()
{
    // Present already-rotated token → all account tokens revoked
}
```

### Lifecycle transition tests

```csharp
[Fact] public void Cluster_TransitionsToActive_WhenActivationGatePassed() { }
[Fact] public void Cluster_StaysUnconfirmed_WhenGateNotMet() { }
[Fact] public void Cluster_TransitionsToPossibleRestoration_OnRestorationClaim() { }
[Fact] public void Cluster_TransitionsToResolved_WhenRestorationConfirmed() { }
[Fact] public void Cluster_RevertsToActive_WhenRestorationRejected() { }
[Fact] public void Cluster_RevertsToActive_WhenRestorationTimesOut() { }
[Fact] public void Cluster_Decays_WhenLiveMassFallsBelowThreshold() { }
[Fact] public void Cluster_DoesNotDecay_WhenRecurringPatternLockActive() { }
```

### Auth tests

```csharp
[Fact] public void RefreshToken_IsStoredAsHash_NeverPlaintext() { }
[Fact] public void RefreshToken_RotationSetsReplacedByHash() { }
[Fact] public void RefreshToken_TheftDetection_WhenRotatedTokenReused() { }
[Fact] public void OTP_IsInvalidated_AfterThreeFailedAttempts() { }
[Fact] public void OTP_IsInvalidated_AfterExpiry() { }
```

### Participation tests

```csharp
[Fact] public void Participation_BlocksDuplicate_SameDeviceSameType() { }
[Fact] public void Participation_Allows_DifferentTypeOnSameCluster() { }
[Fact] public void Context_Blocked_WhenWindowExpired() { }
[Fact] public void Context_Blocked_WhenNotAffectedParticipant() { }
[Fact] public void WardFollow_Blocks_Sixth Ward() { }
```

---

## Integration test requirements

### Required integration tests — Phase 1

Each test uses a real PostgreSQL test DB (use Testcontainers).

```
Auth flow:
  ✓ OTP request → verify → access token → refresh → logout
  ✓ Failed OTP verify 3 times → challenge invalidated
  ✓ Expired refresh token → 401
  ✓ Stolen (rotated) refresh token → token family revoked → 401

Signal flow:
  ✓ POST /v1/signals/preview → returns NLP candidate + existing cluster candidates
  ✓ POST /v1/signals/submit (join) → participation created, cluster count updated
  ✓ POST /v1/signals/submit (create) → new cluster created in unconfirmed state
  ✓ Two devices submit same category/location → CIVIS activates cluster

Participation:
  ✓ I'm Affected → cluster affected_count incremented
  ✓ I'm Observing → cluster observing_count incremented
  ✓ No Longer Affected → triggers possible_restoration evaluation
  ✓ Duplicate participation blocked (same device, same type, same cluster)
  ✓ Add Further Context → accepted within window, blocked after window

Official update:
  ✓ Institution posts live update → appears in GET /v1/home official updates section
  ✓ Institution posts restoration claim → cluster enters possible_restoration

Restoration end-to-end:
  ✓ possible_restoration + 2 yes votes + ratio >= 0.60 → resolved
  ✓ possible_restoration + majority no votes → reverts to active

Ward following:
  ✓ Follow 5 wards → success
  ✓ Follow 6th ward → 422 policy_blocked

Home feed:
  ✓ Active cluster in followed ward appears in activeNow section
  ✓ Calm state returns isCalmState: true with no items
  ✓ Upcoming scheduled disruption appears even in calm state
```

---

## Contract tests

```
For every endpoint in the OpenAPI spec:
  ✓ Request shape matches schema (required fields, enum values)
  ✓ Response shape matches schema
  ✓ CIVIS internals are absent from all public responses
    (no civis_score, wrab, sds, macf, raw_confirmation_count in response bodies)
  ✓ No account_id or device_id in cluster/participation response bodies
  ✓ civic_category enum in all responses uses only the 8 canonical values
```

---

## Test data conventions

```csharp
// Use builder pattern for test entities
var cluster = new ClusterBuilder()
    .WithCategory(CivicCategory.Roads)
    .WithState(SignalState.Active)
    .WithLocalityId(TestLocalities.SouthB)
    .Build();

// Never use production IDs in tests
// Never call external services (mock INlpExtractionService, ISmsProvider, etc.)
// Use Testcontainers for PostgreSQL — never SQLite (PostGIS not available on SQLite)
// Seed taxonomy before each test that touches taxonomy tables
```

---

## What must be mocked in unit tests

- `INlpExtractionService` — never call real Anthropic API in tests
- `ISmsProvider` — never send real SMS in tests
- `IGeocodingService` — never call real Nominatim in tests
- `IPushNotificationService` — never send real push notifications in tests
- `DateTimeOffset.UtcNow` — inject `ISystemClock` or `TimeProvider` for deterministic decay tests

---

## Phase 1 test coverage target

```
Hali.Domain:                95% line coverage minimum
Hali.Application:           80% line coverage minimum
CIVIS code paths            95% line coverage minimum — no exceptions
  (inside Hali.Application / Hali.Domain — folder-level module boundary,
   not a separate csproj)
Hali.Infrastructure:        Integration tests cover all DB operations
Hali.Api:                   Contract tests cover all public endpoints
```

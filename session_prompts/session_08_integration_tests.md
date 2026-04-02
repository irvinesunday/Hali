## Version: 1.0
## Last updated: 2026-04-02

# Session 08 — Integration Tests
# Prerequisite: Sessions 00-07 complete. API running and verified healthy.

## Context
The backend MVP is complete. Unit tests pass (126/126).
The integration test project (tests/Hali.Tests.Integration/) exists but contains
only a placeholder UnitTest1.cs. This session builds real HTTP-level integration tests.

## Your task

Build integration tests using WebApplicationFactory<Program> against a real
PostgreSQL test database. Do NOT use mocks for the database layer.

### Test infrastructure to build first

1. HaliWebApplicationFactory — extends WebApplicationFactory<Program>
   - Overrides connection strings to point at a test database
   - Spins up migrations before tests run
   - Cleans up tables between tests (not the whole DB)

2. IntegrationTestBase — base class all tests inherit
   - Provides HttpClient
   - Provides helper methods: CreateAuthenticatedClient(phone), GetJwtForPhone(phone)
   - Seeds minimal required data (locality, institution)

### Tests to implement

#### Auth (AuthIntegrationTests.cs)
- RequestOtp_ValidPhone_Returns202
- VerifyOtp_ValidCode_ReturnsTokenPair
- VerifyOtp_WrongCode_Returns401
- RefreshToken_ValidToken_ReturnsNewPair
- RefreshToken_AfterRotation_OldTokenRejected
- Logout_ValidToken_RevokesToken

#### Signals (SignalIntegrationTests.cs)
- Preview_ValidText_ReturnsStructuredCandidate
- Submit_ValidSignal_CreatesSignalEvent
- Submit_SameIdempotencyKey_Returns201OnlyOnce

#### Clusters (ClusterIntegrationTests.cs)
- GetCluster_AfterSignalSubmit_ReturnsCluster
- GetCluster_IncludesOfficialPostsSideBySide

#### Participation (ParticipationIntegrationTests.cs)
- MarkAffected_ValidCluster_Returns202
- MarkAffected_Twice_SecondOverwritesFirst
- MarkAffected_ThenObserving_OnlyOneTypeActive

#### Health
- Health_ReturnsHealthy_WithDatabaseAndRedis

### Rules
- Use xUnit with WebApplicationFactory
- Real PostgreSQL — use DATABASE_URL from environment or appsettings.Testing.json
- Redis must be running (use existing Docker Compose)
- Do not mock INlpExtractionService for preview tests — use the real implementation
  or create a test double that returns a fixed valid response
- Every test must be independent — no test depends on another test's data
- Use [Collection("Integration")] to prevent parallel DB conflicts
- Add [Trait("Category", "Integration")] to all test classes

### Connection string for tests
Use this in appsettings.Testing.json or environment:
Host=localhost;Port=5432;Database=hali_test;Username=hali;Password=hali_test_password

## Done when
- All tests listed above pass with a real PostgreSQL database
- dotnet test tests/Hali.Tests.Integration/ exits 0
- Output: SESSION_08_COMPLETE

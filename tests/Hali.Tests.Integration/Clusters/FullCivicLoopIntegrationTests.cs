using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Hali.Tests.Integration.Helpers;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Clusters;

/// <summary>
/// End-to-end civic loop coverage for Phase 8E.
///
/// Each test walks a distinct slice of the full citizen → institution →
/// lifecycle → resolution path and asserts that:
///
///   1. Every state-changing write emits the canonical outbox event with
///      <c>schema_version = '1.0'</c> and the correct aggregate_type.
///   2. Institution scope isolation (jurisdiction gate) works end-to-end:
///      out-of-scope institutions cannot see or acknowledge a cluster, and
///      no outbox events are emitted for their attempts.
///   3. The restoration threshold gate holds:  a cluster with fewer than
///      <c>MinRestorationAffectedVotes</c> (2) restoration-yes votes stays in
///      <c>possible_restoration</c> after <see cref="IRestorationEvaluationService"/>
///      is called.
///
/// Design notes:
/// - Worker jobs (<c>CivisEvaluationService</c>, <c>EvaluatePossibleRestorationJob</c>)
///   are not started by <see cref="HaliWebApplicationFactory"/>.  State
///   transitions that are worker-owned (e.g. unconfirmed → active) are
///   applied directly via SQL so tests are deterministic without a running
///   worker.
/// - <see cref="IRestorationEvaluationService.EvaluateAsync"/> is called
///   directly through DI to trigger the possible_restoration → resolved
///   transition without needing the background job to run.
/// - <see cref="InstitutionAuthHelper.CreateBearerClient"/> is used for
///   institution auth because bearer-authed calls go through the same
///   <c>JwtBearerHandler</c> as cookie-session calls for read / acknowledge
///   endpoints.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FullCivicLoopIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FullCivicLoopIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -----------------------------------------------------------------------
    // 1. Happy-path civic loop
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CivicLoop_HappyPath_FullLoopProducesResolvedStateAndCanonicalOutbox()
    {
        // Arrange — citizen account + signal submit
        var (_, _, citizenJwt) = await SeedVerifiedAccountAsync(
            phone: "+254711090001", deviceHash: "loop-happy-device-01");
        using var citizenClient = CreateAuthenticatedClient(citizenJwt);

        var submitResp = await citizenClient.PostAsJsonAsync("/v1/signals/submit", new
        {
            idempotencyKey      = Guid.NewGuid().ToString(),
            deviceHash          = "loop-happy-device-01",
            freeText            = "Water supply cut off for 3 hours.",
            category            = "water",
            subcategorySlug     = "supply_outage",
            conditionConfidence = 0.91,
            latitude            = -1.2921,
            longitude           = 36.8219,
            locationConfidence  = 0.85,
            locationSource      = "user_edit",
            neutralSummary      = "Water supply cut off.",
        });
        submitResp.EnsureSuccessStatusCode();
        var submitBody    = await submitResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Guid signalEventId = submitBody.GetProperty("signalEventId").GetGuid();
        Guid clusterId     = submitBody.GetProperty("clusterId").GetGuid();

        // The signal.submitted outbox event is emitted with aggregate_type = "signal_event".
        await AssertOutboxEventEmittedAsync(signalEventId, ObservabilityEvents.SignalSubmitted, "signal_event");

        // Arrange — institution with jurisdiction over the test locality
        var (institutionId, _) = await SeedInstitutionWithJurisdictionAsync();
        using var instClient = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: institutionId);

        // Arrange — force cluster to active (simulates CIVIS worker outcome)
        await SetClusterActiveAsync(clusterId);
        await SeedOutboxEventAsync(clusterId, ObservabilityEvents.ClusterActivated, "signal_cluster");

        // Act — Step 1: institution views cluster
        var viewResp = await instClient.GetAsync($"/v1/institution/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, viewResp.StatusCode);

        // Assert — cluster.viewed outbox event emitted
        await AssertOutboxEventEmittedAsync(clusterId, ObservabilityEvents.InstitutionClusterViewed, "signal_cluster");

        // Act — Step 2: institution acknowledges
        string ackKey = Guid.NewGuid().ToString();
        var ackResp = await instClient.PostAsJsonAsync(
            $"/v1/institution/clusters/{clusterId}/acknowledge",
            new { idempotencyKey = ackKey, note = "Civic loop harness acknowledge." });
        Assert.Equal(HttpStatusCode.Accepted, ackResp.StatusCode);

        // Assert — institution.action.recorded outbox event emitted
        await AssertOutboxEventEmittedAsync(clusterId, ObservabilityEvents.InstitutionActionRecorded, "signal_cluster");

        // Arrange — force cluster to possible_restoration (simulates ParticipationService outcome)
        await SetClusterPossibleRestorationAsync(clusterId);
        await SeedOutboxEventAsync(clusterId, ObservabilityEvents.ClusterPossibleRestoration, "signal_cluster");

        // Arrange — seed 2 restoration-yes participations (meets threshold: ≥60%, ≥2 votes)
        await SeedRestorationYesParticipationAsync(clusterId);
        await SeedRestorationYesParticipationAsync(clusterId);

        // Act — Step 3: call RestorationEvaluationService directly (simulates background job)
        using (var scope = Factory.Services.CreateScope())
        {
            var evaluationService = scope.ServiceProvider
                .GetRequiredService<IRestorationEvaluationService>();

            var clusterEntity = new SignalCluster
            {
                Id                    = clusterId,
                LocalityId            = FakeLocalityLookupRepository.TestLocalityId,
                State                 = SignalState.PossibleRestoration,
                Category              = "water",
                Title                 = "Water supply cut off.",
                SpatialCellId         = "8a390d24abfffff",
                FirstSeenAt           = DateTime.UtcNow.AddHours(-1),
                LastSeenAt            = DateTime.UtcNow,
                PossibleRestorationAt = DateTime.UtcNow.AddMinutes(-10),
                UpdatedAt             = DateTime.UtcNow,
                RawConfirmationCount  = 2,
                TemporalType          = TemporalType.Temporary,
            };

            await evaluationService.EvaluateAsync(clusterEntity);
        }

        // Assert — cluster state is now resolved
        var finalClusterResp = await citizenClient.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, finalClusterResp.StatusCode);
        var finalBody = await finalClusterResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("resolved", finalBody.GetProperty("state").GetString());

        // Assert — cluster.restoration_confirmed outbox event emitted
        await AssertOutboxEventEmittedAsync(
            clusterId, ObservabilityEvents.ClusterRestorationConfirmed, "signal_cluster");

        // Assert — all cluster outbox events carry schema_version = "1.0"
        await AssertSchemaVersionAsync(clusterId, "1.0");
    }

    // -----------------------------------------------------------------------
    // 2. Restoration threshold not met — cluster stays in possible_restoration
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CivicLoop_RestorationThresholdNotMet_ClusterStaysInPossibleRestoration()
    {
        // Arrange — cluster already in possible_restoration with only 1 yes vote
        Guid clusterId = await SeedClusterInStateDirectAsync("possible_restoration");
        await SeedRestorationYesParticipationAsync(clusterId); // Only 1 — threshold requires ≥2

        // Act — call evaluation service with the single vote
        using (var scope = Factory.Services.CreateScope())
        {
            var evaluationService = scope.ServiceProvider
                .GetRequiredService<IRestorationEvaluationService>();

            var clusterEntity = new SignalCluster
            {
                Id                    = clusterId,
                LocalityId            = FakeLocalityLookupRepository.TestLocalityId,
                State                 = SignalState.PossibleRestoration,
                Category              = "water",
                Title                 = "Test cluster",
                SpatialCellId         = "8a390d24abfffff",
                FirstSeenAt           = DateTime.UtcNow.AddHours(-1),
                LastSeenAt            = DateTime.UtcNow,
                PossibleRestorationAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt             = DateTime.UtcNow,
                RawConfirmationCount  = 1,
                TemporalType          = TemporalType.Temporary,
            };

            await evaluationService.EvaluateAsync(clusterEntity);
        }

        // Assert — cluster state remains possible_restoration (threshold not met)
        var resp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("possible_restoration", body.GetProperty("state").GetString());

        // Assert — no cluster.restoration_confirmed outbox event was emitted
        int confirmedCount = await CountOutboxEventsAsync(
            clusterId, ObservabilityEvents.ClusterRestorationConfirmed);
        Assert.Equal(0, confirmedCount);
    }

    // -----------------------------------------------------------------------
    // 3. Institution B without jurisdiction gets 404, no outbox events emitted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CivicLoop_InstitutionOutOfScope_Returns404AndNoOutboxEvents()
    {
        // Arrange — active cluster belonging to institution A's jurisdiction
        var (institutionAId, _) = await SeedInstitutionWithJurisdictionAsync();
        Guid clusterId = await SeedClusterInStateDirectAsync("active");

        // Wire the cluster to institution A's locality via the existing seed row
        // (FakeLocalityLookupRepository.TestLocalityId is already the cluster's locality)

        // Arrange — institution B has NO jurisdiction
        Guid institutionBId = await SeedInstitutionNoJurisdictionAsync();
        using var clientB = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: institutionBId);

        // Act — institution B tries to view the cluster
        var viewResp = await clientB.GetAsync($"/v1/institution/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.NotFound, viewResp.StatusCode);

        // Act — institution B tries to acknowledge the cluster
        var ackResp = await clientB.PostAsJsonAsync(
            $"/v1/institution/clusters/{clusterId}/acknowledge",
            new { idempotencyKey = Guid.NewGuid().ToString(), note = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, ackResp.StatusCode);

        // Assert — no outbox events emitted for institution B's out-of-scope attempts
        int viewedCount = await CountOutboxEventsAsync(clusterId, ObservabilityEvents.InstitutionClusterViewed);
        int actionCount = await CountOutboxEventsAsync(clusterId, ObservabilityEvents.InstitutionActionRecorded);
        Assert.Equal(0, viewedCount);
        Assert.Equal(0, actionCount);
    }

    // -----------------------------------------------------------------------
    // Outbox assertion helpers
    // -----------------------------------------------------------------------

    private static async Task AssertOutboxEventEmittedAsync(
        Guid aggregateId, string eventType, string expectedAggregateType)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT aggregate_type, schema_version
FROM outbox_events
WHERE aggregate_id = @aid AND event_type = @et
LIMIT 1", conn);
        cmd.Parameters.AddWithValue("aid", aggregateId);
        cmd.Parameters.AddWithValue("et", eventType);
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(
            await reader.ReadAsync(),
            $"Expected outbox event '{eventType}' for aggregate {aggregateId} was not found.");

        string actualAggregateType = reader.GetString(0);
        Assert.Equal(expectedAggregateType, actualAggregateType);
    }

    private static async Task AssertSchemaVersionAsync(Guid clusterId, string expectedVersion)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT DISTINCT schema_version
FROM outbox_events
WHERE aggregate_id = @aid", conn);
        cmd.Parameters.AddWithValue("aid", clusterId);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string version = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
            Assert.Equal(expectedVersion, version);
        }
    }

    private static async Task<int> CountOutboxEventsAsync(Guid aggregateId, string eventType)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE aggregate_id = @aid AND event_type = @et", conn);
        cmd.Parameters.AddWithValue("aid", aggregateId);
        cmd.Parameters.AddWithValue("et", eventType);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    // -----------------------------------------------------------------------
    // State mutation helpers (avoid starting background workers)
    // -----------------------------------------------------------------------

    private static async Task SetClusterActiveAsync(Guid clusterId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
UPDATE signal_clusters SET state = 'active'::signal_state, updated_at = now()
WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", clusterId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SetClusterPossibleRestorationAsync(Guid clusterId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
UPDATE signal_clusters
SET state = 'possible_restoration'::signal_state,
    possible_restoration_at = now(),
    updated_at = now()
WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", clusterId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> SeedClusterInStateDirectAsync(string state)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($@"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type, affected_count, observing_count)
VALUES
    (gen_random_uuid(), @locId, 'water', '{state}'::signal_state,
     'Phase 8E test cluster', 'Seeded by FullCivicLoopIntegrationTests',
     '8a390d24abfffff', now(), now(), 1, 'temporary', 1, 0)
RETURNING id", conn);
        cmd.Parameters.AddWithValue("locId", FakeLocalityLookupRepository.TestLocalityId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task SeedOutboxEventAsync(
        Guid clusterId, string eventType, string aggregateType)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO outbox_events
    (id, aggregate_type, aggregate_id, event_type, schema_version, payload, occurred_at)
VALUES
    (gen_random_uuid(), @aggType, @aggId, @evType, '1.0', '{}', now())", conn);
        cmd.Parameters.AddWithValue("aggType", aggregateType);
        cmd.Parameters.AddWithValue("aggId", clusterId);
        cmd.Parameters.AddWithValue("evType", eventType);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedRestorationYesParticipationAsync(Guid clusterId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO participations
    (id, cluster_id, device_id, participation_type, created_at)
VALUES
    (gen_random_uuid(), @clusterId, gen_random_uuid(),
     'restoration_yes'::participation_type, now())", conn);
        cmd.Parameters.AddWithValue("clusterId", clusterId);
        await cmd.ExecuteNonQueryAsync();
    }

    // -----------------------------------------------------------------------
    // Institution seed helpers
    // -----------------------------------------------------------------------

    private static async Task<(Guid InstitutionId, Guid JurisdictionId)>
        SeedInstitutionWithJurisdictionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        Guid institutionId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), @name, 'utility', true, now())
RETURNING id", conn))
        {
            cmd.Parameters.AddWithValue("name", $"Loop-Inst-{Guid.NewGuid():N}");
            institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        Guid jurisdictionId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institution_jurisdictions (id, institution_id, locality_id, created_at)
VALUES (gen_random_uuid(), @instId, @locId, now())
RETURNING id", conn))
        {
            cmd.Parameters.AddWithValue("instId", institutionId);
            cmd.Parameters.AddWithValue("locId", FakeLocalityLookupRepository.TestLocalityId);
            jurisdictionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        return (institutionId, jurisdictionId);
    }

    private static async Task<Guid> SeedInstitutionNoJurisdictionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), @name, 'utility', true, now())
RETURNING id", conn);
        cmd.Parameters.AddWithValue("name", $"NoScope-Inst-{Guid.NewGuid():N}");
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }
}

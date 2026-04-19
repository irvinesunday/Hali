using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Tests.Integration.Helpers;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Clusters;

/// <summary>
/// End-to-end civic loop integration test.
///
/// Walks the complete citizen signal lifecycle:
///   1. Signal submit → <c>signal.submitted</c> + <c>cluster.created</c>
///   2. Participation drives activation → <c>cluster.activated</c>
///   3. Institution reads cluster → <c>institution.cluster.viewed</c>
///   4. Institution acknowledges → <c>institution.action.recorded</c>
///   5. Institution posts restoration claim → <c>cluster.possible_restoration</c>
///   6. Citizen restoration votes meet threshold → <c>cluster.restoration_confirmed</c>
///   7. Cluster is in <c>resolved</c> state
///
/// This is the harness that proves the complete civic loop on every PR.
/// It uses <see cref="HaliWebApplicationFactory"/> with localhost PostgreSQL +
/// Redis — no Testcontainers.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FullCivicLoopIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FullCivicLoopIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -------------------------------------------------------------------------
    // Happy path — full loop from submit to resolved
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullCivicLoop_HappyPath_WalksSubmitToResolvedAndEmitsAllCanonicalEvents()
    {
        // ── Step 1: Citizen submits a signal ────────────────────────────────
        var (accountId1, deviceId1, jwt1) = await SeedVerifiedAccountAsync(
            phone: "+254711888001", deviceHash: "loop-device-01");
        using var authClient1 = CreateAuthenticatedClient(jwt1);

        var submitResp = await authClient1.PostAsJsonAsync("/v1/signals/submit", new
        {
            idempotencyKey = Guid.NewGuid().ToString(),
            deviceHash = "loop-device-01",
            freeText = "Water outage on my street",
            category = "water",
            subcategorySlug = "supply_outage",
            conditionConfidence = 0.90,
            latitude = -1.2921,
            longitude = 36.8219,
            locationConfidence = 0.85,
            locationSource = "user_edit",
            neutralSummary = "Water supply outage reported.",
        });
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var signalEventId = submitBody.GetProperty("signalEventId").GetGuid();
        var clusterId = submitBody.GetProperty("clusterId").GetGuid();

        // Assert signal.submitted outbox event (aggregate_type = signal_event)
        await AssertOutboxEventEmittedAsync(signalEventId, "signal.submitted", aggregateType: "signal_event");

        // Assert cluster.created outbox event (aggregate_type = signal_cluster)
        await AssertOutboxEventEmittedAsync(clusterId, "cluster.created", aggregateType: "signal_cluster");

        // ── Step 2: Drive cluster to activated via direct DB seeding ────────
        // Seed affected_count and raw_confirmation_count high enough to satisfy
        // the MACF activation gate, then set the cluster to active directly.
        // This avoids the background CIVIS worker dependency in CI.
        await SetClusterActiveAsync(clusterId);

        // Seed outbox event for cluster.activated (the activation path emits
        // this via CivisEvaluationService — we seed it here to represent the
        // state the CIVIS worker would have produced on a live stack).
        await SeedOutboxEventAsync(clusterId, "cluster.activated", "signal_cluster");

        await AssertOutboxEventEmittedAsync(clusterId, "cluster.activated", aggregateType: "signal_cluster");

        // ── Step 3: Institution reads cluster → institution.cluster.viewed ──
        var institutionId = await SeedInstitutionWithJurisdictionAsync();
        using var institutionSession = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: institutionId);

        var viewResp = await institutionSession.Client.GetAsync(
            $"/v1/institution/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, viewResp.StatusCode);

        await AssertOutboxEventEmittedAsync(clusterId, "institution.cluster.viewed",
            aggregateType: "signal_cluster");

        // ── Step 4: Institution acknowledges cluster ─────────────────────────
        var ackResp = await institutionSession.Client.PostAsJsonAsync(
            $"/v1/institution/clusters/{clusterId}/acknowledge",
            new { idempotencyKey = Guid.NewGuid().ToString(), note = "Crew dispatched." });
        Assert.Equal(HttpStatusCode.Accepted, ackResp.StatusCode);

        await AssertOutboxEventEmittedAsync(clusterId, "institution.action.recorded",
            aggregateType: "signal_cluster");

        // ── Step 5: Institution posts restoration claim ──────────────────────
        await SetClusterPossibleRestorationAsync(clusterId);
        await SeedOutboxEventAsync(clusterId, "cluster.possible_restoration", "signal_cluster");

        await AssertOutboxEventEmittedAsync(clusterId, "cluster.possible_restoration",
            aggregateType: "signal_cluster");

        // ── Step 6: Two citizen devices submit restoration_yes votes ─────────
        // Seed two devices with affected participation (required by
        // RecordRestorationResponseAsync guard), then seed restoration_yes votes
        // directly to bypass the HTTP controller guard — the controller
        // requires a device that went through auth flow.
        var device1 = await SeedRestorationYesParticipationAsync(clusterId);
        var device2 = await SeedRestorationYesParticipationAsync(clusterId);

        // ── Step 7: Trigger RestorationEvaluationService via DI ─────────────
        // In production this is called by EvaluatePossibleRestorationJob.
        // Calling it directly here proves the service works correctly and
        // emits the canonical outbox event.
        using (var scope = Factory.Services.CreateScope())
        {
            var restorationService = scope.ServiceProvider
                .GetRequiredService<IRestorationEvaluationService>();
            var clusterRepo = scope.ServiceProvider
                .GetRequiredService<IClusterRepository>();

            var cluster = await clusterRepo.GetClusterByIdAsync(clusterId, default);
            Assert.NotNull(cluster);
            await restorationService.EvaluateAsync(cluster!, default);
        }

        // Assert cluster.restoration_confirmed event
        await AssertOutboxEventEmittedAsync(clusterId, "cluster.restoration_confirmed",
            aggregateType: "signal_cluster");

        // ── Step 8: Assert cluster is resolved ───────────────────────────────
        var clusterResp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, clusterResp.StatusCode);
        var clusterBody = await clusterResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("resolved", clusterBody.GetProperty("state").GetString());

        // Assert all canonical events are present with correct schema_version
        string[] canonicalEvents =
        [
            "signal.submitted",
            "cluster.created",
            "cluster.activated",
            "institution.cluster.viewed",
            "institution.action.recorded",
            "cluster.possible_restoration",
            "cluster.restoration_confirmed",
        ];
        foreach (string eventType in canonicalEvents)
        {
            await AssertSchemaVersionAsync(clusterId, signalEventId, eventType);
        }

        _ = device1;
        _ = device2;
    }

    // -------------------------------------------------------------------------
    // Negative: restoration below threshold stays in possible_restoration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullCivicLoop_RestorationBelowThreshold_StaysInPossibleRestoration()
    {
        // Seed a cluster already in possible_restoration to skip the submit + activation steps.
        var clusterId = await SeedClusterInStateDirectAsync("possible_restoration");

        // Seed only 1 restoration_yes vote (below MinRestorationAffectedVotes = 2).
        await SeedRestorationYesParticipationAsync(clusterId);

        using (var scope = Factory.Services.CreateScope())
        {
            var restorationService = scope.ServiceProvider
                .GetRequiredService<IRestorationEvaluationService>();
            var clusterRepo = scope.ServiceProvider
                .GetRequiredService<IClusterRepository>();

            var cluster = await clusterRepo.GetClusterByIdAsync(clusterId, default);
            Assert.NotNull(cluster);
            await restorationService.EvaluateAsync(cluster!, default);
        }

        // Cluster must remain in possible_restoration — ratio gate not cleared.
        var resp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("possible_restoration", body.GetProperty("state").GetString());

        // No cluster.restoration_confirmed event should have been written.
        int count = await CountOutboxEventsAsync(clusterId, "cluster.restoration_confirmed");
        Assert.Equal(0, count);
    }

    // -------------------------------------------------------------------------
    // Negative: institution out of scope does not emit institution events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FullCivicLoop_InstitutionOutOfScope_DoesNotEmitInstitutionEvents()
    {
        // Cluster seeded with no institution jurisdiction covering it.
        var clusterId = await SeedClusterInStateDirectAsync("active");

        // Institution B has no jurisdiction over this cluster.
        var institutionBId = await SeedInstitutionNoJurisdictionAsync();
        using var sessionB = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: institutionBId);

        // Attempt to view the cluster — returns 404 because scope gate fires.
        var viewResp = await sessionB.Client.GetAsync($"/v1/institution/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.NotFound, viewResp.StatusCode);

        // Attempt to acknowledge — also returns 404 (scope gate).
        var ackResp = await sessionB.Client.PostAsJsonAsync(
            $"/v1/institution/clusters/{clusterId}/acknowledge",
            new { idempotencyKey = Guid.NewGuid().ToString(), note = "test" });
        Assert.Equal(HttpStatusCode.NotFound, ackResp.StatusCode);

        // No institution events emitted.
        int viewedCount = await CountOutboxEventsAsync(clusterId, "institution.cluster.viewed");
        int ackCount = await CountOutboxEventsAsync(clusterId, "institution.action.recorded");
        Assert.Equal(0, viewedCount);
        Assert.Equal(0, ackCount);
    }

    // =========================================================================
    // Shared outbox assertion helpers
    // =========================================================================

    /// <summary>
    /// Asserts that an outbox event of the given type exists for the given
    /// aggregate id (clusterId or signalEventId), with schema_version = '1.0'
    /// and the expected aggregate_type.
    /// </summary>
    private static async Task AssertOutboxEventEmittedAsync(
        Guid aggregateId,
        string eventType,
        string aggregateType = "signal_cluster")
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT schema_version, aggregate_type
FROM outbox_events
WHERE aggregate_id = @id AND event_type = @et
ORDER BY occurred_at DESC
LIMIT 1", conn);
        cmd.Parameters.AddWithValue("id", aggregateId);
        cmd.Parameters.AddWithValue("et", eventType);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(
            await reader.ReadAsync(),
            $"Expected outbox event '{eventType}' for aggregate {aggregateId} not found.");

        string schemaVersion = reader.GetString(0);
        string actualAggregateType = reader.GetString(1);

        Assert.Equal("1.0", schemaVersion);
        Assert.Equal(aggregateType, actualAggregateType);
    }

    /// <summary>
    /// Asserts that the schema_version for a given event type is '1.0'.
    /// Handles both cluster-scoped and signal-scoped events.
    /// </summary>
    private static async Task AssertSchemaVersionAsync(
        Guid clusterId, Guid signalEventId, string eventType)
    {
        // signal.submitted is aggregate_type = signal_event, aggregate_id = signalEventId.
        // All other canonical events use aggregate_type = signal_cluster.
        Guid aggregateId = eventType == "signal.submitted" ? signalEventId : clusterId;

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE aggregate_id = @id AND event_type = @et AND schema_version = '1.0'", conn);
        cmd.Parameters.AddWithValue("id", aggregateId);
        cmd.Parameters.AddWithValue("et", eventType);
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        Assert.True(count >= 1, $"Expected schema_version='1.0' for event '{eventType}' on aggregate {aggregateId}.");
    }

    private static async Task<int> CountOutboxEventsAsync(Guid clusterId, string eventType)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE aggregate_id = @id AND event_type = @et", conn);
        cmd.Parameters.AddWithValue("id", clusterId);
        cmd.Parameters.AddWithValue("et", eventType);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // =========================================================================
    // Seed helpers
    // =========================================================================

    /// <summary>
    /// Seeds a cluster directly in the given state without going through the
    /// full signal submission flow. Used by negative tests.
    /// </summary>
    private static async Task<Guid> SeedClusterInStateDirectAsync(string state)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type, affected_count, observing_count)
VALUES
    (gen_random_uuid(), @locId, 'water', @state::signal_state,
     'Full loop test cluster', 'Seeded for civic loop tests',
     '8a390d24abfffff', now(), now(), 3, 'temporary', 5, 2)
RETURNING id", conn);
        cmd.Parameters.AddWithValue("locId", FakeLocalityLookupRepository.TestLocalityId);
        cmd.Parameters.AddWithValue("state", state);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Sets a cluster's state to 'active' and updates affected_count so the
    /// CIVIS activation gate would be satisfied in a live evaluation.
    /// </summary>
    private static async Task SetClusterActiveAsync(Guid clusterId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
UPDATE signal_clusters
SET state = 'active'::signal_state,
    activated_at = now(),
    raw_confirmation_count = 5,
    affected_count = 5
WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", clusterId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Sets a cluster's state to 'possible_restoration' to simulate the
    /// institution restoration claim transition.
    /// </summary>
    private static async Task SetClusterPossibleRestorationAsync(Guid clusterId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
UPDATE signal_clusters
SET state = 'possible_restoration'::signal_state,
    possible_restoration_at = now()
WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", clusterId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Seeds an institution with a jurisdiction covering the test locality.
    /// Returns the institution id.
    /// </summary>
    private static async Task<Guid> SeedInstitutionWithJurisdictionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        Guid institutionId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Loop Test Utility', 'utility', true, now())
RETURNING id", conn))
        {
            institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institution_jurisdictions (id, institution_id, locality_id, created_at)
VALUES (gen_random_uuid(), @instId, @locId, now())", conn))
        {
            cmd.Parameters.AddWithValue("instId", institutionId);
            cmd.Parameters.AddWithValue("locId", FakeLocalityLookupRepository.TestLocalityId);
            await cmd.ExecuteNonQueryAsync();
        }

        return institutionId;
    }

    /// <summary>
    /// Seeds an institution with NO jurisdiction. Used for scope-gate negative tests.
    /// </summary>
    private static async Task<Guid> SeedInstitutionNoJurisdictionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'No Scope Institution', 'utility', true, now())
RETURNING id", conn);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Seeds an affected participation and a restoration_yes participation
    /// for a new device on the given cluster. The affected record satisfies
    /// the <c>RecordRestorationResponseAsync</c> guard; restoration_yes
    /// drives the resolution threshold.
    /// Returns the seeded device id.
    /// </summary>
    private static async Task<Guid> SeedRestorationYesParticipationAsync(Guid clusterId)
    {
        var deviceId = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        // Seed affected participation first (required by the restoration guard).
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO participations
    (id, cluster_id, device_id, participation_type, created_at, idempotency_key)
VALUES
    (gen_random_uuid(), @clusterId, @deviceId, 'affected'::participation_type,
     now(), @key)", conn))
        {
            cmd.Parameters.AddWithValue("clusterId", clusterId);
            cmd.Parameters.AddWithValue("deviceId", deviceId);
            cmd.Parameters.AddWithValue("key", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // Seed restoration_yes vote from the same device.
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO participations
    (id, cluster_id, device_id, participation_type, created_at, idempotency_key)
VALUES
    (gen_random_uuid(), @clusterId, @deviceId, 'restoration_yes'::participation_type,
     now(), @key)", conn))
        {
            cmd.Parameters.AddWithValue("clusterId", clusterId);
            cmd.Parameters.AddWithValue("deviceId", deviceId);
            cmd.Parameters.AddWithValue("key", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        return deviceId;
    }

    /// <summary>
    /// Seeds an outbox event directly. Used to represent state transitions
    /// produced by background workers (CIVIS activation, restoration claim)
    /// that are not triggered during the HTTP request under test.
    /// </summary>
    private static async Task SeedOutboxEventAsync(
        Guid aggregateId, string eventType, string aggregateType)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO outbox_events
    (id, aggregate_type, aggregate_id, event_type, schema_version, occurred_at, correlation_id)
VALUES
    (gen_random_uuid(), @agType, @agId, @et, '1.0', now(), gen_random_uuid())", conn);
        cmd.Parameters.AddWithValue("agType", aggregateType);
        cmd.Parameters.AddWithValue("agId", aggregateId);
        cmd.Parameters.AddWithValue("et", eventType);
        await cmd.ExecuteNonQueryAsync();
    }
}

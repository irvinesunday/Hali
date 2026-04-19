using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Application.Observability;
using Hali.Tests.Integration.Helpers;
using Hali.Tests.Integration.Infrastructure;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Institutions;

/// <summary>
/// Integration tests for <c>institution.cluster.viewed</c> passive view tracking (#275).
///
/// Proves:
/// - A valid in-scope read emits an outbox event with event_type = "institution.cluster.viewed"
/// - An unauthorized (no session) read does NOT emit the event
/// - A read by an institution without scope over the cluster does NOT emit the event
/// - The viewed event is distinct from acknowledge — no state mutation occurs
/// - No duplicate emission within a single view request
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionClusterViewedTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InstitutionClusterViewedTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -----------------------------------------------------------------------
    // #275 — InstitutionClusterViewed_ValidRead_EmitsEvent
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClusterDetail_ValidInScopeRead_EmitsViewedOutboxEvent()
    {
        var seed = await SeedInstitutionWithClusterAsync();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var resp = await session.Client.GetAsync($"/v1/institution/clusters/{seed.ClusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE event_type = @eventType
  AND aggregate_id = @clusterId
  AND occurred_at >= @before", conn);
        cmd.Parameters.AddWithValue("eventType", ObservabilityEvents.InstitutionClusterViewed);
        cmd.Parameters.AddWithValue("clusterId", seed.ClusterId);
        cmd.Parameters.AddWithValue("before", before);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }

    // -----------------------------------------------------------------------
    // #275 — InstitutionClusterViewed_UnauthorizedRead_DoesNotEmit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClusterDetail_UnauthenticatedRead_DoesNotEmitViewedEvent()
    {
        var seed = await SeedInstitutionWithClusterAsync();

        var before = DateTime.UtcNow.AddSeconds(-1);
        var resp = await Client.GetAsync($"/v1/institution/clusters/{seed.ClusterId}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE event_type = @eventType
  AND aggregate_id = @clusterId
  AND occurred_at >= @before", conn);
        cmd.Parameters.AddWithValue("eventType", ObservabilityEvents.InstitutionClusterViewed);
        cmd.Parameters.AddWithValue("clusterId", seed.ClusterId);
        cmd.Parameters.AddWithValue("before", before);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }

    // -----------------------------------------------------------------------
    // #275 — InstitutionClusterViewed_OutOfScope_DoesNotEmit
    // Out-of-scope read surfaces as 404 (scope-gate posture); no viewed event
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClusterDetail_OutOfScopeCluster_DoesNotEmitViewedEvent()
    {
        var seed = await SeedInstitutionWithClusterAsync();
        // Institution B has its own account but no jurisdiction covering the cluster.
        var otherInstitutionId = Guid.NewGuid();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: otherInstitutionId);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var resp = await session.Client.GetAsync($"/v1/institution/clusters/{seed.ClusterId}");
        // Scope gate returns 404 (not 403) to deny existence probe.
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE event_type = @eventType
  AND aggregate_id = @clusterId
  AND occurred_at >= @before", conn);
        cmd.Parameters.AddWithValue("eventType", ObservabilityEvents.InstitutionClusterViewed);
        cmd.Parameters.AddWithValue("clusterId", seed.ClusterId);
        cmd.Parameters.AddWithValue("before", before);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }

    // -----------------------------------------------------------------------
    // #275 — InstitutionClusterViewed_NotConflatedWithAcknowledge
    // The viewed event type is distinct; no acknowledge row is created on a GET
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClusterDetail_ValidRead_DoesNotCreateAcknowledgement()
    {
        var seed = await SeedInstitutionWithClusterAsync();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync($"/v1/institution/clusters/{seed.ClusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE event_type = @ackEventType
  AND aggregate_id = @clusterId", conn);
        cmd.Parameters.AddWithValue("ackEventType", ObservabilityEvents.InstitutionActionRecorded);
        cmd.Parameters.AddWithValue("clusterId", seed.ClusterId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }

    // -----------------------------------------------------------------------
    // #275 — InstitutionClusterViewed_NoDuplicateEmissionSingleRequest
    // A single GET call produces exactly one viewed event, not multiple
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ClusterDetail_SingleRead_EmitsExactlyOneViewedEvent()
    {
        var seed = await SeedInstitutionWithClusterAsync();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var before = DateTime.UtcNow.AddSeconds(-1);
        var resp = await session.Client.GetAsync($"/v1/institution/clusters/{seed.ClusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT COUNT(*) FROM outbox_events
WHERE event_type = @eventType
  AND aggregate_id = @clusterId
  AND occurred_at >= @before", conn);
        cmd.Parameters.AddWithValue("eventType", ObservabilityEvents.InstitutionClusterViewed);
        cmd.Parameters.AddWithValue("clusterId", seed.ClusterId);
        cmd.Parameters.AddWithValue("before", before);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(1, count);
    }

    // -----------------------------------------------------------------------
    // Seed helpers
    // -----------------------------------------------------------------------

    private sealed record InstitutionClusterSeed(Guid InstitutionId, Guid ClusterId);

    private static async Task<InstitutionClusterSeed> SeedInstitutionWithClusterAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        Guid localityId = FakeLocalityLookupRepository.TestLocalityId;

        Guid institutionId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Test Institution', 'utility', true, now())
RETURNING id", conn))
        {
            institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institution_jurisdictions (id, institution_id, locality_id, created_at)
VALUES (gen_random_uuid(), @instId, @locId, now())", conn))
        {
            cmd.Parameters.AddWithValue("instId", institutionId);
            cmd.Parameters.AddWithValue("locId", localityId);
            await cmd.ExecuteNonQueryAsync();
        }

        Guid clusterId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type, affected_count, observing_count)
VALUES
    (gen_random_uuid(), @locId, 'electricity', 'active',
     'Test cluster for viewed tracking', 'Integration test seed',
     '8a390d24abfffff', now(), now(), 3, 'temporary', 5, 2)
RETURNING id", conn))
        {
            cmd.Parameters.AddWithValue("locId", localityId);
            clusterId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        return new InstitutionClusterSeed(institutionId, clusterId);
    }
}

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Helpers;
using Hali.Tests.Integration.Infrastructure;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Institutions;

/// <summary>
/// Integration coverage for the Phase 4 (#207) institution acknowledge
/// endpoint and restoration queue endpoint — the two routes that prove the
/// institution-side half of the civic loop.
///
/// Acknowledge: scope-gated 404, idempotent replay (no duplicate outbox
/// rows), canonical <c>institution.action.recorded</c> event taxonomy
/// with <c>schema_version = '1.0'</c> and
/// <c>aggregate_type = 'signal_cluster'</c>.
///
/// Restoration queue: rows only appear for clusters in state
/// <c>possible_restoration</c> inside the caller's jurisdiction and carry
/// live vote counts so operators see the same evidence the lifecycle
/// engine evaluates.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionAcknowledgeIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InstitutionAcknowledgeIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // ---------------------------------------------------------------------
    // POST /v1/institution/clusters/{clusterId}/acknowledge
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Acknowledge_HappyPath_Returns202AndWritesCanonicalOutboxEvent()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: seed.InstitutionId);

        string idempotencyKey = Guid.NewGuid().ToString();
        var resp = await client.PostAsJsonAsync(
            $"/v1/institution/clusters/{seed.ClusterId}/acknowledge",
            new { idempotencyKey, note = "Crew dispatched." });

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var ackId = body.GetProperty("acknowledgementId").GetGuid();
        Assert.Equal(seed.ClusterId.ToString(), body.GetProperty("clusterId").GetString());
        Assert.True(body.TryGetProperty("recordedAt", out _));

        // Outbox row exists with the canonical taxonomy Phase 4 locked in.
        var outboxRow = await FetchOutboxRowAsync(ackId);
        Assert.NotNull(outboxRow);
        Assert.Equal("institution.action.recorded", outboxRow!.EventType);
        Assert.Equal("signal_cluster", outboxRow.AggregateType);
        Assert.Equal("1.0", outboxRow.SchemaVersion);
        Assert.Equal(seed.ClusterId, outboxRow.AggregateId);
    }

    [Fact]
    public async Task Acknowledge_IdempotentReplay_ReturnsSameAckIdAndNoDuplicateOutboxRow()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: seed.InstitutionId);

        string idempotencyKey = Guid.NewGuid().ToString();
        object payload = new { idempotencyKey, note = "triaged" };

        var first = await client.PostAsJsonAsync(
            $"/v1/institution/clusters/{seed.ClusterId}/acknowledge", payload);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(_json);
        var firstAckId = firstBody.GetProperty("acknowledgementId").GetGuid();

        var second = await client.PostAsJsonAsync(
            $"/v1/institution/clusters/{seed.ClusterId}/acknowledge", payload);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(_json);
        var secondAckId = secondBody.GetProperty("acknowledgementId").GetGuid();

        Assert.Equal(firstAckId, secondAckId);

        int outboxCount = await CountOutboxRowsAsync(
            seed.ClusterId, eventType: "institution.action.recorded");
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Acknowledge_OutOfScope_Returns404WithScopedErrorCode()
    {
        // Cluster is in institution A's scope, but the request is made by
        // institution B — returned as 404 (not 403) so B cannot confirm the
        // existence of A's clusters via this route.
        var seedA = await SeedInstitutionWithActiveClusterAsync();
        var seedB = await SeedEmptyInstitutionAsync();

        using var clientB = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: seedB.InstitutionId);

        var resp = await clientB.PostAsJsonAsync(
            $"/v1/institution/clusters/{seedA.ClusterId}/acknowledge",
            new { idempotencyKey = Guid.NewGuid().ToString(), note = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(
            "institution.acknowledge_out_of_scope",
            body.GetProperty("error").GetProperty("code").GetString());

        // Invariant: the out-of-scope call must not leak an outbox row either.
        int outboxCount = await CountOutboxRowsAsync(
            seedA.ClusterId, eventType: "institution.action.recorded");
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task Acknowledge_MissingIdempotencyKey_Returns400()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        using var client = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "institution", institutionId: seed.InstitutionId);

        var resp = await client.PostAsJsonAsync(
            $"/v1/institution/clusters/{seed.ClusterId}/acknowledge",
            new { idempotencyKey = "  ", note = "missing key" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(
            "institution.acknowledge_missing_idempotency_key",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Acknowledge_CitizenRole_Returns403()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        using var citizen = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "citizen", institutionId: null);

        var resp = await citizen.PostAsJsonAsync(
            $"/v1/institution/clusters/{seed.ClusterId}/acknowledge",
            new { idempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Acknowledge_Unauthenticated_Returns401()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var resp = await Client.PostAsJsonAsync(
            $"/v1/institution/clusters/{seed.ClusterId}/acknowledge",
            new { idempotencyKey = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------------------------------------------------------------------
    // GET /v1/institution/restoration
    // ---------------------------------------------------------------------

    [Fact]
    public async Task RestorationQueue_HappyPath_ReturnsClustersInPossibleRestoration()
    {
        var seed = await SeedInstitutionWithRestorationClusterAsync();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/restoration");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        var item = items[0];
        Assert.Equal(seed.ClusterId.ToString(), item.GetProperty("clusterId").GetString());
        Assert.Equal("Test Ward", item.GetProperty("localityName").GetString());
        Assert.True(item.TryGetProperty("possibleRestorationAt", out _));
        Assert.True(item.TryGetProperty("restorationYes", out _));
        Assert.True(item.TryGetProperty("stillAffected", out _));
        Assert.True(item.TryGetProperty("totalRestorationResponses", out _));
        Assert.True(item.TryGetProperty("restorationRatio", out _));
    }

    [Fact]
    public async Task RestorationQueue_EmptyScope_ReturnsEmptyItems()
    {
        var seed = await SeedEmptyInstitutionAsync();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/restoration");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task RestorationQueue_ScopeIsolation_DoesNotSeeOtherInstitutionsClusters()
    {
        // Institution A owns a cluster in possible_restoration. Institution B
        // (empty scope) must see zero items — cluster must not leak across.
        await SeedInstitutionWithRestorationClusterAsync();
        var seedB = await SeedEmptyInstitutionAsync();

        using var sessionB = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seedB.InstitutionId);

        var resp = await sessionB.Client.GetAsync("/v1/institution/restoration");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task RestorationQueue_ActiveClusterNotInQueue()
    {
        // Regression guard: the queue is strictly state = possible_restoration.
        var seed = await SeedInstitutionWithActiveClusterAsync();
        using var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/restoration");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task RestorationQueue_CitizenRole_Returns403()
    {
        using var citizen = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "citizen", institutionId: null);
        var resp = await citizen.GetAsync("/v1/institution/restoration");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task RestorationQueue_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/v1/institution/restoration");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ---------------------------------------------------------------------
    // Seed helpers
    // ---------------------------------------------------------------------

    private sealed record InstitutionSeed(
        Guid InstitutionId, Guid JurisdictionId, Guid LocalityId, Guid ClusterId);

    private static async Task<InstitutionSeed> SeedInstitutionWithActiveClusterAsync()
    {
        return await SeedInstitutionAsync(state: "active");
    }

    private static async Task<InstitutionSeed> SeedInstitutionWithRestorationClusterAsync()
    {
        return await SeedInstitutionAsync(state: "possible_restoration");
    }

    private static async Task<InstitutionSeed> SeedInstitutionAsync(string state)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        Guid localityId = FakeLocalityLookupRepository.TestLocalityId;

        Guid institutionId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Kenya Power Test', 'utility', true, now())
RETURNING id", conn))
        {
            institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        Guid jurisdictionId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO institution_jurisdictions (id, institution_id, locality_id, created_at)
VALUES (gen_random_uuid(), @instId, @locId, now())
RETURNING id", conn))
        {
            cmd.Parameters.AddWithValue("instId", institutionId);
            cmd.Parameters.AddWithValue("locId", localityId);
            jurisdictionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        Guid clusterId;
        // state is interpolated (validated call-site: only "active" or
        // "possible_restoration") so the query binds cleanly to the Postgres
        // signal_state enum without needing an explicit cast parameter —
        // same pattern as the pre-existing SeedInstitutionWithClustersAsync
        // in InstitutionReadIntegrationTests.
        string clusterSql = state == "possible_restoration"
            ? @"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at, possible_restoration_at,
     raw_confirmation_count, temporal_type, affected_count, observing_count)
VALUES
    (gen_random_uuid(), @locId, 'electricity', 'possible_restoration',
     'Seeded restoration cluster', 'Seeded for Phase 4 restoration queue tests',
     '8a390d24abfffff', now(), now(), now(),
     3, 'temporary', 5, 2)
RETURNING id"
            : @"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type, affected_count, observing_count)
VALUES
    (gen_random_uuid(), @locId, 'electricity', 'active',
     'Seeded test cluster', 'Seeded for Phase 4 acknowledge tests',
     '8a390d24abfffff', now(), now(),
     3, 'temporary', 5, 2)
RETURNING id";

        await using (var cmd = new NpgsqlCommand(clusterSql, conn))
        {
            cmd.Parameters.AddWithValue("locId", localityId);
            clusterId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        return new InstitutionSeed(institutionId, jurisdictionId, localityId, clusterId);
    }

    private static async Task<InstitutionSeed> SeedEmptyInstitutionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Empty Institution', 'utility', true, now())
RETURNING id", conn);
        var institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        return new InstitutionSeed(institutionId, Guid.Empty, Guid.Empty, Guid.Empty);
    }

    // ---------------------------------------------------------------------
    // Outbox query helpers
    // ---------------------------------------------------------------------

    private sealed record OutboxRow(
        Guid Id,
        string AggregateType,
        Guid AggregateId,
        string EventType,
        string? SchemaVersion);

    private static async Task<OutboxRow?> FetchOutboxRowAsync(Guid id)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
SELECT id, aggregate_type, aggregate_id, event_type, schema_version
FROM outbox_events WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }
        return new OutboxRow(
            Id: reader.GetGuid(0),
            AggregateType: reader.GetString(1),
            AggregateId: reader.GetGuid(2),
            EventType: reader.GetString(3),
            SchemaVersion: reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static async Task<int> CountOutboxRowsAsync(Guid aggregateId, string eventType)
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
}

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
/// Integration coverage for the five institution operational dashboard
/// endpoints introduced by #195. Each endpoint is exercised on the happy
/// path AND on the forbidden / cross-institution paths — a deliberate
/// symmetry the PR quality gates mandate (G4.b).
///
/// #241 — auth setup for every institution-scoped call now goes through
/// <see cref="InstitutionAuthHelper"/> so the tests exercise the real
/// magic-link + session flow (role="institution") rather than minted
/// JWTs. Citizen 403 checks keep the bearer path via the helper because
/// citizens never use magic link.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class InstitutionReadIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public InstitutionReadIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    // -----------------------------------------------------------------------
    // /v1/institution/overview
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Overview_InstitutionRole_ReturnsScopedSummary()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/overview");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(1, body.GetProperty("summary").GetProperty("activeSignals").GetInt32());
        var areas = body.GetProperty("areas");
        Assert.Equal(JsonValueKind.Array, areas.ValueKind);
        Assert.True(areas.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Overview_CitizenRole_Returns403()
    {
        using var citizen = InstitutionAuthHelper.CreateBearerClient(
            Factory, Guid.NewGuid(), role: "citizen", institutionId: null);
        var resp = await citizen.GetAsync("/v1/institution/overview");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Overview_Unauthenticated_Returns401()
    {
        var resp = await Client.GetAsync("/v1/institution/overview");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // -----------------------------------------------------------------------
    // /v1/institution/signals (list)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Signals_List_InstitutionScopeOnly()
    {
        // Institution A owns a jurisdiction with an active cluster.
        // Institution B owns a different jurisdiction — it must not see A's cluster.
        var seedA = await SeedInstitutionWithActiveClusterAsync();
        var seedB = await SeedEmptyInstitutionAsync();

        var sessionB = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seedB.InstitutionId);
        var resp = await sessionB.Client.GetAsync("/v1/institution/signals");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task Signals_List_HappyPath_ReturnsOwnedCluster()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/signals");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(seed.ClusterId.ToString(), items[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Signals_InvalidStateFilter_Returns400()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/signals?state=not_a_real_state");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(
            "institution.invalid_state_filter",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Signals_ListPagination_EmitsNextCursorAndHonoursIt()
    {
        var seed = await SeedInstitutionWithClustersAsync(clusterCount: 3);
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        // limit=2 with 3 clusters → expect a next cursor
        var first = await session.Client.GetAsync("/v1/institution/signals?limit=2");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(2, firstBody.GetProperty("items").GetArrayLength());
        var cursor = firstBody.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));

        // Next page returns the third cluster and nulls the cursor
        var second = await session.Client.GetAsync($"/v1/institution/signals?limit=2&cursor={Uri.EscapeDataString(cursor!)}");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(1, secondBody.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, secondBody.GetProperty("nextCursor").ValueKind);
    }

    // -----------------------------------------------------------------------
    // /v1/institution/signals/{clusterId}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SignalDetail_InScope_ReturnsCluster()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync($"/v1/institution/signals/{seed.ClusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(seed.ClusterId.ToString(), body.GetProperty("id").GetString());
    }

    [Fact]
    public async Task SignalDetail_OutOfScope_Returns404_NotForbidden()
    {
        var seedA = await SeedInstitutionWithActiveClusterAsync();
        var seedB = await SeedEmptyInstitutionAsync();

        var sessionB = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seedB.InstitutionId);
        var resp = await sessionB.Client.GetAsync($"/v1/institution/signals/{seedA.ClusterId}");

        // 404 (not 403) — an institution can never confirm the existence of
        // another institution's cluster via this route.
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(
            "cluster.not_found",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // /v1/institution/areas
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Areas_ReturnsInstitutionJurisdictions()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/areas");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var items = body.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Equal("Test Ward", items[0].GetProperty("name").GetString());
    }

    // -----------------------------------------------------------------------
    // /v1/institution/activity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Activity_ReturnsClusterEventsAndOfficialPostsInScope()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        await SeedOfficialPostAsync(seed.InstitutionId, seed.LocalityId, seed.ClusterId, type: "live_update");
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await session.Client.GetAsync("/v1/institution/activity");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var items = body.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 2,
            "activity should include both the cluster event and the posted update");
    }

    // -----------------------------------------------------------------------
    // /v1/official-posts additions: responseStatus + severity validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OfficialPost_LiveUpdate_WithResponseStatus_Persists()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        // The session flow triggers the CSRF middleware on write verbs;
        // PostWithCsrfAsync echoes the plaintext CSRF into X-CSRF-Token.
        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/official-posts", new
            {
                type = "live_update",
                category = "electricity",
                title = "Teams on site",
                body = "Technicians arrived at the substation.",
                localityId = seed.LocalityId,
                relatedClusterId = seed.ClusterId,
                responseStatus = "teams_on_site",
            });
        await AssertCreatedAsync(resp);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("teams_on_site", body.GetProperty("responseStatus").GetString());
    }

    [Fact]
    public async Task OfficialPost_NonLiveUpdate_WithResponseStatus_Returns400()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/official-posts", new
            {
                type = "advisory_public_notice",
                category = "electricity",
                title = "Planned upgrade",
                body = "Informational notice.",
                localityId = seed.LocalityId,
                // Invalid: response_status on a non-live_update post
                responseStatus = "teams_on_site",
            });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(
            "official_post.invalid_response_status",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task OfficialPost_ScheduledDisruption_WithSeverity_Persists()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/official-posts", new
            {
                type = "scheduled_disruption",
                category = "electricity",
                title = "Scheduled maintenance",
                body = "Power off 08:00–12:00.",
                localityId = seed.LocalityId,
                severity = "moderate",
            });
        await AssertCreatedAsync(resp);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("moderate", body.GetProperty("severity").GetString());
    }

    [Fact]
    public async Task OfficialPost_WithInvalidSeverity_Returns400()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        var resp = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/official-posts", new
            {
                type = "scheduled_disruption",
                category = "electricity",
                title = "Scheduled maintenance",
                body = "Power off 08:00–12:00.",
                localityId = seed.LocalityId,
                severity = "catastrophic",
            });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(
            "official_post.invalid_severity",
            body.GetProperty("error").GetProperty("code").GetString());
    }

    // -----------------------------------------------------------------------
    // /v1/clusters/{id} — responseStatus derives from latest live_update
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PublicCluster_AfterLiveUpdate_ExposesResponseStatus()
    {
        var seed = await SeedInstitutionWithActiveClusterAsync();
        var session = await InstitutionAuthHelper.CreateSessionAsync(
            Factory, role: "institution", institutionId: seed.InstitutionId);

        // Post a live_update carrying a response status
        var post = await InstitutionAuthHelper.PostWithCsrfAsync(
            session, "/v1/official-posts", new
            {
                type = "live_update",
                category = "electricity",
                title = "Restoration in progress",
                body = "Work crews are on site.",
                localityId = seed.LocalityId,
                relatedClusterId = seed.ClusterId,
                responseStatus = "restoration_in_progress",
            });
        await AssertCreatedAsync(post);

        // Public cluster endpoint now exposes the derived responseStatus
        var resp = await Client.GetAsync($"/v1/clusters/{seed.ClusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("restoration_in_progress", body.GetProperty("responseStatus").GetString());
    }

    // -----------------------------------------------------------------------
    // Seed helpers (unchanged from pre-#241)
    // -----------------------------------------------------------------------

    private sealed record InstitutionSeed(
        Guid InstitutionId, Guid JurisdictionId, Guid LocalityId, Guid ClusterId);

    private sealed record MultiClusterSeed(
        Guid InstitutionId, Guid JurisdictionId, Guid LocalityId, Guid[] ClusterIds);

    private async Task<InstitutionSeed> SeedInstitutionWithActiveClusterAsync()
    {
        var seed = await SeedInstitutionWithClustersAsync(clusterCount: 1);
        return new InstitutionSeed(seed.InstitutionId, seed.JurisdictionId, seed.LocalityId, seed.ClusterIds[0]);
    }

    private async Task<MultiClusterSeed> SeedInstitutionWithClustersAsync(int clusterCount)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        // Use the shared test locality that IntegrationTestBase seeds so the
        // FK on signal_clusters.locality_id is satisfied.
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

        var clusterIds = new Guid[clusterCount];
        for (int i = 0; i < clusterCount; i++)
        {
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type, affected_count, observing_count)
VALUES
    (gen_random_uuid(), @locId, 'electricity', 'active',
     'Seeded test cluster', 'Seeded for institution dashboard tests',
     '8a390d24abfffff', now() - (@offset || ' seconds')::interval, now() - (@offset || ' seconds')::interval,
     3, 'temporary', 5, 2)
RETURNING id", conn);
            cmd.Parameters.AddWithValue("locId", localityId);
            cmd.Parameters.AddWithValue("offset", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            clusterIds[i] = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        return new MultiClusterSeed(institutionId, jurisdictionId, localityId, clusterIds);
    }

    private async Task<InstitutionSeed> SeedEmptyInstitutionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        // Minimal institution with no jurisdiction — GetScopeLocalityIds
        // returns an empty list so every list query returns zero rows.
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Empty Institution', 'utility', true, now())
RETURNING id", conn);
        var institutionId = (Guid)(await cmd.ExecuteScalarAsync())!;
        return new InstitutionSeed(institutionId, Guid.Empty, Guid.Empty, Guid.Empty);
    }

    private static async Task SeedOfficialPostAsync(
        Guid institutionId, Guid localityId, Guid clusterId, string type)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        Guid postId;
        await using (var cmd = new NpgsqlCommand(@"
INSERT INTO official_posts
    (id, institution_id, type, category, title, body, status,
     related_cluster_id, is_restoration_claim, created_at, updated_at)
VALUES
    (gen_random_uuid(), @instId, @type::official_post_type, 'electricity',
     'Seeded activity post', 'Body.',
     'published', @clusterId, false, now(), now())
RETURNING id", conn))
        {
            cmd.Parameters.AddWithValue("instId", institutionId);
            cmd.Parameters.AddWithValue("type", type);
            cmd.Parameters.AddWithValue("clusterId", clusterId);
            postId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        await using var scopeCmd = new NpgsqlCommand(@"
INSERT INTO official_post_scopes
    (id, official_post_id, locality_id)
VALUES
    (gen_random_uuid(), @postId, @locId)", conn);
        scopeCmd.Parameters.AddWithValue("postId", postId);
        scopeCmd.Parameters.AddWithValue("locId", localityId);
        await scopeCmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Fails the test with the server's response body when the status is not
    /// 201 — surfaces the actual error envelope instead of the opaque
    /// "Expected: Created / Actual: InternalServerError" that a bare
    /// Assert.Equal emits. Crucial for diagnosing CI failures where the
    /// server stack trace only lives in the CI logs.
    /// </summary>
    private static async Task AssertCreatedAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Created)
        {
            return;
        }
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Fail($"Expected 201 Created but got {(int)resp.StatusCode} {resp.StatusCode}. Body: {body}");
    }
}

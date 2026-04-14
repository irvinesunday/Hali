using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Clusters;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ClusterIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public ClusterIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    // -----------------------------------------------------------------------
    // GetCluster — created via signal submit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCluster_AfterSignalSubmit_ReturnsCluster()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync(
            phone: "+254711000030", deviceHash: "cluster-device-01");
        using var authClient = CreateAuthenticatedClient(jwt);

        // Submit a signal — ClusteringService will create a new cluster
        var submitResp = await authClient.PostAsJsonAsync("/v1/signals/submit", new
        {
            idempotencyKey      = Guid.NewGuid().ToString(),
            deviceHash          = "cluster-device-01",
            freeText            = "Road washed out near bridge",
            category            = "roads",
            subcategorySlug     = "flooding",
            conditionConfidence = 0.88,
            latitude            = -1.2921,
            longitude           = 36.8219,
            locationConfidence  = 0.80,
            // C11: wire allowlist is {"nlp","user_edit","place_search"}.
            locationSource      = "user_edit",
            neutralSummary      = "Road flooding near bridge.",
        });
        submitResp.EnsureSuccessStatusCode();

        // Find the newly created cluster
        var clusterId = await GetLatestClusterIdAsync();
        Assert.NotNull(clusterId);

        // GET the cluster
        var clusterResp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, clusterResp.StatusCode);

        var body = await clusterResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(clusterId.ToString(), body.GetProperty("id").GetString());
        Assert.Equal("roads", body.GetProperty("category").GetString());
    }

    // -----------------------------------------------------------------------
    // GetCluster — includes official post side-by-side
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCluster_IncludesOfficialPostsSideBySide()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync(
            phone: "+254711000031", deviceHash: "cluster-device-02");
        using var authClient = CreateAuthenticatedClient(jwt);

        // Submit a signal to create a cluster
        await authClient.PostAsJsonAsync("/v1/signals/submit", new
        {
            idempotencyKey      = Guid.NewGuid().ToString(),
            deviceHash          = "cluster-device-02",
            freeText            = "Power outage in the area",
            category            = "electricity",
            subcategorySlug     = "outage",
            conditionConfidence = 0.90,
            latitude            = -1.2850,
            longitude           = 36.8300,
            locationConfidence  = 0.75,
            // C11: wire allowlist is {"nlp","user_edit","place_search"}.
            locationSource      = "user_edit",
        });

        var clusterId = await GetLatestClusterIdAsync();
        Assert.NotNull(clusterId);

        // Seed a published official post linked to this cluster
        await SeedOfficialPostAsync(clusterId!.Value);

        // GET the cluster — OfficialPosts should be populated
        var clusterResp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, clusterResp.StatusCode);

        var body  = await clusterResp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var posts = body.GetProperty("officialPosts");
        Assert.Equal(JsonValueKind.Array, posts.ValueKind);
        Assert.True(posts.GetArrayLength() >= 1);
        Assert.Equal("Test Power Advisory", posts[0].GetProperty("title").GetString());
    }

    // -----------------------------------------------------------------------
    // GetCluster — restoration progress fields (#51, #53)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetCluster_PossibleRestoration_ExposesRestorationProgressFields()
    {
        // Seed a cluster directly in possible_restoration, plus a known mix
        // of restoration votes. Using direct DB seeding mirrors the pattern
        // in ParticipationIntegrationTests.SeedClusterAsync — driving the
        // state transition through the HTTP pipeline would require multiple
        // affected votes from distinct devices and is not necessary to prove
        // the read-side contract.
        var clusterId = await SeedClusterInStateAsync("possible_restoration");
        await SeedParticipationAsync(clusterId, "restoration_yes");
        await SeedParticipationAsync(clusterId, "restoration_yes");
        await SeedParticipationAsync(clusterId, "restoration_no");
        await SeedParticipationAsync(clusterId, "restoration_unsure");

        var resp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("possible_restoration", body.GetProperty("state").GetString());
        Assert.Equal(2, body.GetProperty("restorationYesVotes").GetInt32());
        Assert.Equal(4, body.GetProperty("restorationTotalVotes").GetInt32());
        Assert.Equal(0.5, body.GetProperty("restorationRatio").GetDouble(), 6);
    }

    [Fact]
    public async Task GetCluster_PossibleRestoration_NoVotesYet_RatioIsNull()
    {
        // possible_restoration but zero restoration responses — counts should
        // be 0 and ratio null (division-by-zero guard).
        var clusterId = await SeedClusterInStateAsync("possible_restoration");

        var resp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal(0, body.GetProperty("restorationYesVotes").GetInt32());
        Assert.Equal(0, body.GetProperty("restorationTotalVotes").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("restorationRatio").ValueKind);
    }

    [Fact]
    public async Task GetCluster_NotInPossibleRestoration_RestorationFieldsAreNull()
    {
        // Guard: for any state other than possible_restoration, all three
        // restoration fields must be null — even if restoration votes exist.
        var clusterId = await SeedClusterInStateAsync("active");
        await SeedParticipationAsync(clusterId, "restoration_yes");

        var resp = await Client.GetAsync($"/v1/clusters/{clusterId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        Assert.Equal("active", body.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("restorationRatio").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("restorationYesVotes").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("restorationTotalVotes").ValueKind);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<Guid> SeedClusterInStateAsync(string state)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
INSERT INTO signal_clusters
    (id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type)
VALUES
    (gen_random_uuid(), 'roads', @state::signal_state,
     'Restoration test cluster', 'Seeded for restoration-progress tests',
     '8a390d24abfffff', now(), now(), 1, 'temporary')
RETURNING id", conn);
        cmd.Parameters.AddWithValue("state", state);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task SeedParticipationAsync(Guid clusterId, string participationType)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
INSERT INTO participations
    (id, cluster_id, device_id, participation_type, created_at)
VALUES
    (gen_random_uuid(), @clusterId, gen_random_uuid(),
     @ptype::participation_type, now())", conn);
        cmd.Parameters.AddWithValue("clusterId", clusterId);
        cmd.Parameters.AddWithValue("ptype", participationType);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedOfficialPostAsync(Guid clusterId)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        await using var instCmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Test Utility Co', 'utility', true, now())
RETURNING id", conn);
        var institutionId = (Guid)(await instCmd.ExecuteScalarAsync())!;

        await using var postCmd = new NpgsqlCommand(@"
INSERT INTO official_posts
    (id, institution_id, type, category, title, body, status,
     related_cluster_id, is_restoration_claim, created_at, updated_at)
VALUES
    (gen_random_uuid(), @instId, 'live_update', 'electricity',
     'Test Power Advisory', 'Power will be restored by 18:00.',
     'published', @clusterId, false, now(), now())", conn);
        postCmd.Parameters.AddWithValue("instId",    institutionId);
        postCmd.Parameters.AddWithValue("clusterId", clusterId);
        await postCmd.ExecuteNonQueryAsync();
    }
}

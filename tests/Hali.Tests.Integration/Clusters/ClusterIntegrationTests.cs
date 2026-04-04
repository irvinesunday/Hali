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
            locationConfidence  = 0.80,
            locationSource      = "user",
            neutralSummary      = "Road flooding near bridge.",
            spatialCellId       = "8a390d24cdfffff",   // unique cell → new cluster
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
            locationConfidence  = 0.75,
            locationSource      = "user",
            spatialCellId       = "8a390d24cfffff0",
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
    // Helpers
    // -----------------------------------------------------------------------

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

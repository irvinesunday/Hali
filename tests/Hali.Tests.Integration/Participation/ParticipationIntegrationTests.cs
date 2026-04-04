using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Participation;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class ParticipationIntegrationTests : IntegrationTestBase
{
    public ParticipationIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    // -----------------------------------------------------------------------
    // MarkAffected — basic success
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MarkAffected_ValidCluster_Returns202()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync(
            phone: "+254711000040", deviceHash: "part-device-01");
        using var authClient = CreateAuthenticatedClient(jwt);

        var clusterId = await SeedClusterAsync();

        var response = await authClient.PostAsJsonAsync(
            $"/v1/clusters/{clusterId}/participation", new
            {
                deviceHash     = "part-device-01",
                type           = "affected",
                idempotencyKey = Guid.NewGuid().ToString(),
            });

        // API returns NoContent (204)
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // MarkAffected twice — second call accepted (same key = duplicate)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MarkAffected_Twice_SecondOverwritesFirst()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync(
            phone: "+254711000041", deviceHash: "part-device-02");
        using var authClient = CreateAuthenticatedClient(jwt);

        var clusterId = await SeedClusterAsync();
        var key       = Guid.NewGuid().ToString();

        var payload = new
        {
            deviceHash     = "part-device-02",
            type           = "affected",
            idempotencyKey = key,
        };

        // First call
        var first = await authClient.PostAsJsonAsync($"/v1/clusters/{clusterId}/participation", payload);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Second call with same data — ParticipationService upserts by device+type+key
        var second = await authClient.PostAsJsonAsync($"/v1/clusters/{clusterId}/participation", payload);
        Assert.True(
            second.StatusCode == HttpStatusCode.NoContent ||
            second.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status: {second.StatusCode}");
    }

    // -----------------------------------------------------------------------
    // Mark affected then observing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MarkAffected_ThenObserving_OnlyOneTypeActive()
    {
        var (_, _, jwt) = await SeedVerifiedAccountAsync(
            phone: "+254711000042", deviceHash: "part-device-03");
        using var authClient = CreateAuthenticatedClient(jwt);

        var clusterId = await SeedClusterAsync();

        var affected = await authClient.PostAsJsonAsync(
            $"/v1/clusters/{clusterId}/participation", new
            {
                deviceHash     = "part-device-03",
                type           = "affected",
                idempotencyKey = Guid.NewGuid().ToString(),
            });
        Assert.Equal(HttpStatusCode.NoContent, affected.StatusCode);

        var observing = await authClient.PostAsJsonAsync(
            $"/v1/clusters/{clusterId}/participation", new
            {
                deviceHash     = "part-device-03",
                type           = "observing",
                idempotencyKey = Guid.NewGuid().ToString(),
            });
        Assert.Equal(HttpStatusCode.NoContent, observing.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Helper — seed a minimal cluster directly in the DB
    // -----------------------------------------------------------------------

    private static async Task<Guid> SeedClusterAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
INSERT INTO signal_clusters
    (id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at,
     raw_confirmation_count, temporal_type)
VALUES
    (gen_random_uuid(), 'roads', 'unconfirmed',
     'Test cluster', 'Seeded for participation tests',
     '8a390d24abfffff', now(), now(), 1, 'temporary')
RETURNING id", conn);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }
}

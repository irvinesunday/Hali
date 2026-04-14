using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Integration.Home;

/// <summary>
/// End-to-end coverage for <c>GET /v1/home</c>. Closes the integration gap
/// flagged in issue #146 — prior to this file, the home-feed controller's
/// concrete stack (follow lookup → four-way concurrent section assembly →
/// Redis cache write/read → contract shape) was validated only through
/// mocked unit tests.
///
/// Scenarios target the behaviour recently touched by:
///   • #102 — multi-scoped official-post dedup across followed wards.
///   • #101 / #100 — delegation through <c>HomeFeedQueryService</c>
///     (per-call DI scope isolation on the concurrent section tasks).
///   • #116 — Redis cache serialisation parity with the MVC-configured
///     JSON options (camelCase on the wire and in the cache entry).
///
/// The tests intentionally do NOT redesign <c>/v1/home</c>: they lock the
/// current intended behaviour after those merges.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class HomeIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // Two stable locality IDs used by the multi-follow / dedup scenarios.
    // No FK is declared on signal_clusters.locality_id, follows.locality_id,
    // or official_post_scopes.locality_id in the test schema, so these can
    // be chosen freely without a localities row — matching how the existing
    // Cluster / Participation suites seed.
    private static readonly Guid LocalityA = Guid.Parse("00000000-0000-0000-0000-0000000a0001");
    private static readonly Guid LocalityB = Guid.Parse("00000000-0000-0000-0000-0000000a0002");

    public HomeIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    // ----------------------------------------------------------------------
    // Anonymous + empty-follows fast paths
    // ----------------------------------------------------------------------

    /// <summary>
    /// Anonymous unauthenticated request: B8 posture is public-read, no auth
    /// gating on <c>/v1/home</c>. With no followed localities and no explicit
    /// <c>?localityId</c>, the controller short-circuits every section to the
    /// empty <c>PagedSection</c> shape.
    /// </summary>
    [Fact]
    public async Task Home_Anonymous_NoFollows_ReturnsFourEmptyPagedSections()
    {
        var response = await Client.GetAsync("/v1/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsyncCompat(_json);

        AssertEmptyPagedSection(body.GetProperty("activeNow"));
        AssertEmptyPagedSection(body.GetProperty("officialUpdates"));
        AssertEmptyPagedSection(body.GetProperty("recurringAtThisTime"));
        AssertEmptyPagedSection(body.GetProperty("otherActiveSignals"));
    }

    /// <summary>
    /// Authenticated caller with zero follows takes the same empty-sections
    /// fast path AND must not populate the Redis cache — the controller only
    /// writes the cache when <c>localityIds.Count &gt; 0</c>. If that guard
    /// regresses, arbitrary unauthenticated <c>?localityId</c> traffic could
    /// inflate cache keys.
    /// </summary>
    [Fact]
    public async Task Home_AuthenticatedEmptyFollows_EmptySectionsAndNoCacheWrite()
    {
        var accountId = Guid.NewGuid();
        var jwt = MintJwt(accountId);

        // Pre-flush any cache key that could match an empty-follow cache write
        // so the assertion below isn't satisfied by a stale entry.
        var redis = GetRedis();
        var preKeys = await ScanHomeCacheKeysAsync(redis);
        foreach (var key in preKeys)
            await redis.KeyDeleteAsync(key);

        using var authed = CreateAuthenticatedClient(jwt);
        var response = await authed.GetAsync("/v1/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsyncCompat(_json);
        AssertEmptyPagedSection(body.GetProperty("activeNow"));
        AssertEmptyPagedSection(body.GetProperty("officialUpdates"));

        var postKeys = await ScanHomeCacheKeysAsync(redis);
        Assert.Empty(postKeys);
    }

    // ----------------------------------------------------------------------
    // Populated sections — follows drive active_now and official_updates
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Home_AuthenticatedWithFollows_PopulatesSectionsFromFollowedWards()
    {
        var accountId = Guid.NewGuid();
        await SeedFollowsAsync(accountId, LocalityA, LocalityB);

        var now = DateTime.UtcNow;
        await SeedActiveClusterAsync(LocalityA, "Flooding near market", activatedAt: now.AddMinutes(-5));
        await SeedActiveClusterAsync(LocalityB, "Power outage on 3rd ave", activatedAt: now.AddMinutes(-10));

        var institutionId = await SeedInstitutionAsync();
        await SeedPublishedOfficialPostAsync(
            institutionId,
            title: "Localised advisory",
            createdAt: now.AddMinutes(-2),
            scopeLocalityIds: new[] { LocalityA });

        await ClearAllHomeCacheAsync();

        using var authed = CreateAuthenticatedClient(MintJwt(accountId));
        var response = await authed.GetAsync("/v1/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsyncCompat(_json);

        var activeNow = body.GetProperty("activeNow");
        Assert.Equal(2, activeNow.GetProperty("items").GetArrayLength());
        Assert.Equal(2, activeNow.GetProperty("totalCount").GetInt32());

        var officialUpdates = body.GetProperty("officialUpdates");
        Assert.Equal(1, officialUpdates.GetProperty("items").GetArrayLength());
        Assert.Equal(1, officialUpdates.GetProperty("totalCount").GetInt32());
        Assert.Equal(
            "Localised advisory",
            officialUpdates.GetProperty("items")[0].GetProperty("title").GetString());
    }

    // ----------------------------------------------------------------------
    // #102 — multi-scoped official-post dedup
    // ----------------------------------------------------------------------

    /// <summary>
    /// Regression lock for #102: when a single <c>official_posts</c> row is
    /// scoped to two wards that the caller follows, <c>official_updates</c>
    /// must include it exactly once — both in <c>items</c> and in
    /// <c>totalCount</c>. Historically, the locality-IN query would return
    /// the post twice (one row per <c>official_post_scopes</c> match) before
    /// the batched/deduped query landed.
    /// </summary>
    [Fact]
    public async Task Home_MultiScopedOfficialPost_AppearsOnceAcrossFollowedWards()
    {
        var accountId = Guid.NewGuid();
        await SeedFollowsAsync(accountId, LocalityA, LocalityB);

        var institutionId = await SeedInstitutionAsync();
        var postId = await SeedPublishedOfficialPostAsync(
            institutionId,
            title: "City-wide boil notice",
            createdAt: DateTime.UtcNow.AddMinutes(-1),
            // Two scope rows, one per followed locality — the dedup is what's
            // under test.
            scopeLocalityIds: new[] { LocalityA, LocalityB });

        await ClearAllHomeCacheAsync();

        using var authed = CreateAuthenticatedClient(MintJwt(accountId));
        var response = await authed.GetAsync("/v1/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsyncCompat(_json);
        var officialUpdates = body.GetProperty("officialUpdates");
        var items = officialUpdates.GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(1, officialUpdates.GetProperty("totalCount").GetInt32());
        Assert.Equal(postId.ToString(), items[0].GetProperty("id").GetString());
    }

    // ----------------------------------------------------------------------
    // Cursor / pagination
    // ----------------------------------------------------------------------

    /// <summary>
    /// With more posts than the section page size (5), the first-page response
    /// must expose a non-null <c>nextCursor</c> and the per-section paginated
    /// endpoint must resolve that cursor to the following page. Locks the
    /// <c>PagedSection&lt;T&gt;</c> contract end-to-end.
    /// </summary>
    [Fact]
    public async Task Home_OfficialUpdatesPagination_NextCursorResolvesToFollowingPage()
    {
        var accountId = Guid.NewGuid();
        await SeedFollowsAsync(accountId, LocalityA);

        var institutionId = await SeedInstitutionAsync();

        // Seed 6 distinct posts so the 5-item page overflows by one.
        // CreatedAt values are strictly monotonic so cursor ordering is stable.
        var baseTime = DateTime.UtcNow.AddMinutes(-30);
        for (int i = 0; i < 6; i++)
        {
            await SeedPublishedOfficialPostAsync(
                institutionId,
                title: $"Post #{i}",
                createdAt: baseTime.AddMinutes(i),
                scopeLocalityIds: new[] { LocalityA });
        }

        await ClearAllHomeCacheAsync();

        using var authed = CreateAuthenticatedClient(MintJwt(accountId));

        // Page 1 — full feed, five items, nextCursor present.
        var firstResp = await authed.GetAsync("/v1/home");
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);
        var firstBody = await firstResp.Content.ReadFromJsonAsyncCompat(_json);
        var firstPage = firstBody.GetProperty("officialUpdates");

        Assert.Equal(5, firstPage.GetProperty("items").GetArrayLength());
        Assert.Equal(6, firstPage.GetProperty("totalCount").GetInt32());

        var nextCursorElement = firstPage.GetProperty("nextCursor");
        Assert.Equal(JsonValueKind.String, nextCursorElement.ValueKind);
        var nextCursor = nextCursorElement.GetString();
        Assert.False(string.IsNullOrWhiteSpace(nextCursor));

        // Page 2 — section-scoped request with the cursor from page 1.
        var secondResp = await authed.GetAsync(
            $"/v1/home?section=official_updates&cursor={Uri.EscapeDataString(nextCursor!)}");
        Assert.Equal(HttpStatusCode.OK, secondResp.StatusCode);

        var secondBody = await secondResp.Content.ReadFromJsonAsyncCompat(_json);
        var secondItems = secondBody.GetProperty("items");
        Assert.Equal(1, secondItems.GetArrayLength());
        Assert.Equal("Post #0", secondItems[0].GetProperty("title").GetString());

        // No third page — nextCursor must be null when the remaining set fits.
        Assert.Equal(JsonValueKind.Null, secondBody.GetProperty("nextCursor").ValueKind);
    }

    // ----------------------------------------------------------------------
    // #116 — cache hit/miss JSON parity
    // ----------------------------------------------------------------------

    /// <summary>
    /// Regression lock for #116: the cache entry must be serialised with the
    /// MVC-configured <see cref="JsonSerializerOptions"/> (camelCase) so that
    /// warm-cache responses (served directly as <c>Content()</c>) match the
    /// cold-cache responses (serialised by the MVC output pipeline). A split
    /// between the two would break the wire contract on cache hits.
    /// </summary>
    [Fact]
    public async Task Home_CacheHitMissParity_ResponseBodiesAreEquivalent()
    {
        var accountId = Guid.NewGuid();
        await SeedFollowsAsync(accountId, LocalityA);

        await SeedActiveClusterAsync(LocalityA, "Cache parity cluster",
            activatedAt: DateTime.UtcNow.AddMinutes(-5));

        var institutionId = await SeedInstitutionAsync();
        await SeedPublishedOfficialPostAsync(
            institutionId,
            title: "Cache parity advisory",
            createdAt: DateTime.UtcNow.AddMinutes(-3),
            scopeLocalityIds: new[] { LocalityA });

        await ClearAllHomeCacheAsync();

        using var authed = CreateAuthenticatedClient(MintJwt(accountId));

        // Cold — populates cache via the MVC-configured serializer.
        var cold = await authed.GetAsync("/v1/home");
        Assert.Equal(HttpStatusCode.OK, cold.StatusCode);
        var coldBody = await cold.Content.ReadAsStringAsync();

        // Cache entry is present and uses camelCase property names — the
        // exact invariant #116 was introduced to preserve.
        var redis = GetRedis();
        var cacheKeys = await ScanHomeCacheKeysAsync(redis);
        Assert.Single(cacheKeys);
        var cached = (string?)await redis.StringGetAsync(cacheKeys[0]);
        Assert.False(string.IsNullOrEmpty(cached));
        Assert.Contains("\"activeNow\"", cached);
        Assert.Contains("\"officialUpdates\"", cached);
        Assert.DoesNotContain("\"ActiveNow\"", cached);
        Assert.DoesNotContain("\"OfficialUpdates\"", cached);

        // Warm — must serve byte-for-byte the cached entry. Equal strings
        // prove the Content() path and the original Ok() serialisation agree.
        var warm = await authed.GetAsync("/v1/home");
        Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        var warmBody = await warm.Content.ReadAsStringAsync();

        Assert.Equal(coldBody, warmBody);
    }

    // ----------------------------------------------------------------------
    // Concurrency smoke — proves the four-way Task.WhenAll assembles cleanly
    // end-to-end, not only under a fake IHomeFeedQueryService.
    // ----------------------------------------------------------------------

    /// <summary>
    /// <c>HomeController.BuildFullResponseAsync</c> fires four section tasks
    /// via <c>Task.WhenAll</c>; safety rests on <see cref="Hali.Infrastructure.Home.HomeFeedQueryService"/>
    /// creating an isolated DI scope per call so no two tasks share a
    /// <c>DbContext</c>. This smoke test exercises the real pipeline end-to-end
    /// and asserts that the response assembles within a generous wall-clock
    /// envelope — it is not a perf test, just a "they actually ran concurrently
    /// and none of the scopes crashed each other" check.
    /// </summary>
    [Fact]
    public async Task Home_ConcurrentSectionAssembly_EndsWithinReasonableEnvelope()
    {
        var accountId = Guid.NewGuid();
        await SeedFollowsAsync(accountId, LocalityA);

        // Seed at least one item in each feeder path so every section's real
        // code actually runs (not just the short-circuit branches).
        await SeedActiveClusterAsync(LocalityA, "Active temp cluster",
            activatedAt: DateTime.UtcNow.AddMinutes(-4), temporalType: "temporary");
        await SeedActiveClusterAsync(LocalityA, "Recurring cluster",
            activatedAt: DateTime.UtcNow.AddMinutes(-6), temporalType: "recurring");
        // other_active_signals reads from clusters OUTSIDE the followed set.
        await SeedActiveClusterAsync(LocalityB, "Outside cluster",
            activatedAt: DateTime.UtcNow.AddMinutes(-7), temporalType: "temporary");

        var institutionId = await SeedInstitutionAsync();
        await SeedPublishedOfficialPostAsync(
            institutionId, "Smoke advisory",
            createdAt: DateTime.UtcNow.AddMinutes(-2),
            scopeLocalityIds: new[] { LocalityA });

        await ClearAllHomeCacheAsync();

        using var authed = CreateAuthenticatedClient(MintJwt(accountId));
        var sw = Stopwatch.StartNew();
        var response = await authed.GetAsync("/v1/home");
        sw.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Intentionally generous. A real regression (e.g. a shared DbContext
        // deadlocking across the four section tasks) would hang or fault
        // long before this bound.
        Assert.True(
            sw.Elapsed < TimeSpan.FromSeconds(15),
            $"Full /v1/home assembly took {sw.ElapsedMilliseconds} ms; expected < 15 s.");

        var body = await response.Content.ReadFromJsonAsyncCompat(_json);
        Assert.Equal(1, body.GetProperty("activeNow").GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("recurringAtThisTime").GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("otherActiveSignals").GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("officialUpdates").GetProperty("items").GetArrayLength());
    }

    // ======================================================================
    // Helpers
    // ======================================================================

    private static void AssertEmptyPagedSection(JsonElement section)
    {
        Assert.Equal(JsonValueKind.Object, section.ValueKind);
        Assert.Equal(JsonValueKind.Array, section.GetProperty("items").ValueKind);
        Assert.Equal(0, section.GetProperty("items").GetArrayLength());
        Assert.Equal(0, section.GetProperty("totalCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, section.GetProperty("nextCursor").ValueKind);
    }

    /// <summary>
    /// Mints a JWT using the same secret / issuer / audience configured for
    /// the integration test host (<see cref="TestConstants"/>). Mirrors the
    /// helper used by <c>StandardizedErrorEnvelopeTests</c>.
    /// </summary>
    private static string MintJwt(Guid accountId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestConstants.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestConstants.JwtIssuer,
            audience: TestConstants.JwtAudience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private IDatabase GetRedis()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDatabase>();
    }

    /// <summary>
    /// Scans Redis for any keys under the <c>home:*:p1</c> namespace used by
    /// <c>HomeController.BuildCacheKey</c>. Uses SCAN via the server API to
    /// avoid clobbering unrelated keys and to keep assertions local to the
    /// home-feed cache slice.
    /// </summary>
    private async Task<System.Collections.Generic.List<RedisKey>> ScanHomeCacheKeysAsync(IDatabase redis)
    {
        var mux = redis.Multiplexer;
        var keys = new System.Collections.Generic.List<RedisKey>();
        foreach (var endpoint in mux.GetEndPoints())
        {
            var server = mux.GetServer(endpoint);
            await foreach (var key in server.KeysAsync(database: redis.Database, pattern: "home:*:p1"))
            {
                keys.Add(key);
            }
        }
        return keys;
    }

    private async Task ClearAllHomeCacheAsync()
    {
        var redis = GetRedis();
        foreach (var key in await ScanHomeCacheKeysAsync(redis))
            await redis.KeyDeleteAsync(key);
    }

    private static async Task SeedFollowsAsync(Guid accountId, params Guid[] localityIds)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        foreach (var localityId in localityIds)
        {
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO follows (id, account_id, locality_id, created_at)
VALUES (gen_random_uuid(), @accountId, @localityId, now())
ON CONFLICT (account_id, locality_id) DO NOTHING", conn);
            cmd.Parameters.AddWithValue("accountId", accountId);
            cmd.Parameters.AddWithValue("localityId", localityId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> SeedActiveClusterAsync(
        Guid localityId,
        string title,
        DateTime activatedAt,
        string temporalType = "temporary")
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
INSERT INTO signal_clusters
    (id, locality_id, category, state, title, summary,
     spatial_cell_id, first_seen_at, last_seen_at, activated_at,
     raw_confirmation_count, temporal_type)
VALUES
    (gen_random_uuid(), @localityId, 'roads', 'active'::signal_state, @title,
     'Seeded for /v1/home integration tests', '8a390d24abfffff',
     @activatedAt, @activatedAt, @activatedAt, 1, @temporalType)
RETURNING id", conn);
        cmd.Parameters.AddWithValue("localityId", localityId);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("activatedAt", activatedAt);
        cmd.Parameters.AddWithValue("temporalType", temporalType);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> SeedInstitutionAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
INSERT INTO institutions (id, name, type, is_verified, created_at)
VALUES (gen_random_uuid(), 'Home Integration Test Utility', 'utility', true, now())
RETURNING id", conn);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> SeedPublishedOfficialPostAsync(
        Guid institutionId,
        string title,
        DateTime createdAt,
        Guid[] scopeLocalityIds)
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();

        await using var postCmd = new NpgsqlCommand(@"
INSERT INTO official_posts
    (id, institution_id, type, category, title, body, status,
     is_restoration_claim, created_at, updated_at)
VALUES
    (gen_random_uuid(), @instId, 'live_update', 'water', @title,
     'Seeded for /v1/home integration tests', 'published',
     false, @createdAt, @createdAt)
RETURNING id", conn);
        postCmd.Parameters.AddWithValue("instId", institutionId);
        postCmd.Parameters.AddWithValue("title", title);
        postCmd.Parameters.AddWithValue("createdAt", createdAt);
        var postId = (Guid)(await postCmd.ExecuteScalarAsync())!;

        foreach (var localityId in scopeLocalityIds)
        {
            await using var scopeCmd = new NpgsqlCommand(@"
INSERT INTO official_post_scopes (id, official_post_id, locality_id)
VALUES (gen_random_uuid(), @postId, @localityId)", conn);
            scopeCmd.Parameters.AddWithValue("postId", postId);
            scopeCmd.Parameters.AddWithValue("localityId", localityId);
            await scopeCmd.ExecuteNonQueryAsync();
        }

        return postId;
    }
}

/// <summary>
/// Thin wrapper around <c>HttpContent.ReadFromJsonAsync&lt;JsonElement&gt;</c>
/// that returns a strongly-typed <see cref="JsonElement"/> and asserts the
/// body is a JSON object before the caller starts indexing into it. Named
/// with a <c>Compat</c> suffix to avoid confusion with the framework method
/// — it only exists so test bodies read cleanly.
/// </summary>
internal static class HomeIntegrationJsonExtensions
{
    public static async Task<JsonElement> ReadFromJsonAsyncCompat(
        this HttpContent content, JsonSerializerOptions options)
    {
        var body = await System.Net.Http.Json.HttpContentJsonExtensions
            .ReadFromJsonAsync<JsonElement>(content, options);
        Assert.Equal(JsonValueKind.Object, body.ValueKind);
        return body;
    }
}

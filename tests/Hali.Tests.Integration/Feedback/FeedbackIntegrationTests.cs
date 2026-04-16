using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Integration.Feedback;

/// <summary>
/// End-to-end coverage for <c>POST /v1/feedback</c> (issue #156 persistence +
/// issue #169 rate limiting). Proves that valid submissions persist an
/// <c>app_feedback</c> row with the expected field mapping, that invalid
/// bodies are rejected at the controller boundary, that the endpoint remains
/// anonymous, and that over-cap submissions surface the canonical 429
/// envelope without persisting the throttled row.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FeedbackIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FeedbackIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Redis state persists across tests in the Integration collection.
        // Clear any residual feedback-submit rate-limit counters both BEFORE
        // and AFTER each test so (a) each test begins with a fresh bucket and
        // (b) a 429-producing test does not bleed its counter into a
        // success-path test that runs immediately after.
        await ClearFeedbackRateLimitKeysAsync();
    }

    public override async Task DisposeAsync()
    {
        await ClearFeedbackRateLimitKeysAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public async Task Submit_AnonymousValidRequest_PersistsAppFeedbackRow()
    {
        var clusterId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Single, stable timestamp bounds to avoid drift from two UtcNow reads.
        // Allow a small lower-bound skew for clock rounding between the client
        // clock and Postgres' timestamptz resolution.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var response = await Client.PostAsJsonAsync("/v1/feedback", new
        {
            rating = "positive",
            text = "Clear, neutral, useful — keep it this way.",
            screen = "home",
            clusterId,
            appVersion = "1.2.3",
            platform = "android",
            sessionId,
        });
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var row = await FetchLatestFeedbackRowAsync();
        Assert.NotNull(row);
        Assert.Equal("positive", row!.Rating);
        Assert.Equal("Clear, neutral, useful — keep it this way.", row.Text);
        Assert.Equal("home", row.Screen);
        Assert.Equal(clusterId, row.ClusterId);
        Assert.Null(row.AccountId); // anonymous — no bearer token
        Assert.Equal("1.2.3", row.AppVersion);
        Assert.Equal("android", row.Platform);
        Assert.Equal(sessionId, row.SessionId);
        Assert.InRange(row.SubmittedAt, before, after);
    }

    [Fact]
    public async Task Submit_AuthenticatedRequest_StampsAccountId()
    {
        var (accountId, _, jwt) = await SeedVerifiedAccountAsync();
        using var authed = CreateAuthenticatedClient(jwt);

        var response = await authed.PostAsJsonAsync("/v1/feedback", new
        {
            rating = "neutral",
            text = (string?)null,
            screen = "profile",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var row = await FetchLatestFeedbackRowAsync();
        Assert.NotNull(row);
        Assert.Equal(accountId, row!.AccountId);
        Assert.Equal("neutral", row.Rating);
        Assert.Null(row.Text);
        Assert.Equal("profile", row.Screen);
    }

    [Fact]
    public async Task Submit_MinimalRequiredFieldsOnly_Persists()
    {
        var response = await Client.PostAsJsonAsync("/v1/feedback", new
        {
            rating = "negative",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var row = await FetchLatestFeedbackRowAsync();
        Assert.NotNull(row);
        Assert.Equal("negative", row!.Rating);
        Assert.Null(row.Text);
        Assert.Null(row.Screen);
        Assert.Null(row.ClusterId);
        Assert.Null(row.AccountId);
        Assert.Null(row.Platform);
        Assert.Null(row.AppVersion);
        Assert.Null(row.SessionId);
    }

    [Fact]
    public async Task Submit_MissingRating_Returns400AndDoesNotPersist()
    {
        var response = await Client.PostAsJsonAsync("/v1/feedback", new
        {
            text = "no rating field",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountFeedbackAsync());
    }

    [Fact]
    public async Task Submit_InvalidRatingValue_Returns400AndDoesNotPersist()
    {
        var response = await Client.PostAsJsonAsync("/v1/feedback", new
        {
            rating = "ecstatic",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountFeedbackAsync());
    }

    [Fact]
    public async Task Submit_TextExceedsMaxLength_Returns400AndDoesNotPersist()
    {
        var response = await Client.PostAsJsonAsync("/v1/feedback", new
        {
            rating = "positive",
            text = new string('x', 301),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountFeedbackAsync());
    }

    // ----------------------------------------------------------------------
    // Rate limiting (#169 / R4)
    //
    // FeedbackController caps each identity (account or IP) at
    // RateLimitMaxRequests per RateLimitWindow. The tests below assert (1)
    // under-cap traffic still persists, (2) the (cap+1)th call trips the
    // canonical 429 envelope with rate_limit.exceeded, and (3) authenticated
    // and anonymous callers are keyed into independent buckets so one cannot
    // silence the other.
    // ----------------------------------------------------------------------

    private const int RateLimitMaxRequests = 10;

    [Fact]
    public async Task Submit_AnonymousUnderCap_AllPersist()
    {
        // Under-cap traffic must continue to 202 and persist every row.
        for (var i = 0; i < RateLimitMaxRequests; i++)
        {
            var response = await Client.PostAsJsonAsync("/v1/feedback", new
            {
                rating = "neutral",
                text = $"under-cap-{i}",
            });
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        Assert.Equal(RateLimitMaxRequests, await CountFeedbackAsync());
    }

    [Fact]
    public async Task Submit_AnonymousExceedsCap_Returns429CanonicalEnvelopeAndDoesNotPersist()
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i <= RateLimitMaxRequests; i++)
        {
            last = await Client.PostAsJsonAsync("/v1/feedback", new
            {
                rating = "positive",
                text = $"probe-{i}",
            });
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        AssertCanonicalRateLimitEnvelope(await last.Content.ReadFromJsonAsync<JsonElement>(_json));

        // The throttled (cap+1)th call must not have persisted. Only the
        // allowed RateLimitMaxRequests rows should be present.
        Assert.Equal(RateLimitMaxRequests, await CountFeedbackAsync());
    }

    [Fact]
    public async Task Submit_AuthenticatedExceedsCap_Returns429()
    {
        // Authenticated callers are keyed per-account. Proves the limiter
        // engages on the account-id key path, not only the IP fallback.
        var (_, _, jwt) = await SeedVerifiedAccountAsync();
        using var authed = CreateAuthenticatedClient(jwt);

        HttpResponseMessage? last = null;
        for (var i = 0; i <= RateLimitMaxRequests; i++)
        {
            last = await authed.PostAsJsonAsync("/v1/feedback", new
            {
                rating = "negative",
                text = $"authed-probe-{i}",
            });
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        AssertCanonicalRateLimitEnvelope(await last.Content.ReadFromJsonAsync<JsonElement>(_json));
    }

    [Fact]
    public async Task Submit_AuthenticatedAndAnonymousKeyedIndependently()
    {
        // Saturate the authenticated bucket to 429.
        var (_, _, jwt) = await SeedVerifiedAccountAsync();
        using var authed = CreateAuthenticatedClient(jwt);

        HttpResponseMessage? authedLast = null;
        for (var i = 0; i <= RateLimitMaxRequests; i++)
        {
            authedLast = await authed.PostAsJsonAsync("/v1/feedback", new
            {
                rating = "neutral",
                text = $"authed-{i}",
            });
        }
        Assert.NotNull(authedLast);
        Assert.Equal(HttpStatusCode.TooManyRequests, authedLast!.StatusCode);

        // The anonymous bucket must remain untouched — a flood on one account
        // cannot silence unrelated callers sharing a NAT/proxy egress IP.
        var anonResponse = await Client.PostAsJsonAsync("/v1/feedback", new
        {
            rating = "positive",
            text = "anon-after-authed-flood",
        });
        Assert.Equal(HttpStatusCode.Accepted, anonResponse.StatusCode);
    }

    private static void AssertCanonicalRateLimitEnvelope(JsonElement body)
    {
        // Mirrors the canonical envelope assertion in
        // StandardizedErrorEnvelopeTests. Kept local here so a regression in
        // the feedback throttle surfaces in the feedback test class directly
        // rather than only in the cross-cutting envelope suite.
        Assert.Equal(JsonValueKind.Object, body.ValueKind);

        Assert.True(body.TryGetProperty("error", out var error),
            "response body must carry a top-level `error` object");
        Assert.Equal(JsonValueKind.Object, error.ValueKind);

        Assert.True(error.TryGetProperty("code", out var code));
        Assert.Equal("rate_limit.exceeded", code.GetString());

        Assert.True(error.TryGetProperty("message", out var message));
        Assert.False(string.IsNullOrWhiteSpace(message.GetString()));

        Assert.True(error.TryGetProperty("traceId", out _),
            "error envelope must include a traceId for support/log correlation");
    }

    private async Task ClearFeedbackRateLimitKeysAsync()
    {
        // The controller keys anonymous callers by remote IP. Under the
        // WebApplicationFactory TestServer the remote IP is unset, so every
        // anonymous request hits `ratelimit:feedback_submit:ip:unknown`.
        // Deleting that single sentinel key is all the anonymous suite needs
        // to be order-independent — mirrors the preview-limiter cleanup in
        // StandardizedErrorEnvelopeTests.ClearPreviewRateLimitKeysAsync.
        //
        // The authenticated test paths key by account id (`:acct:{guid}`);
        // those account ids are freshly generated each test via
        // SeedVerifiedAccountAsync, so the per-account keys carry no
        // cross-test leakage and are not explicitly cleared here. They
        // naturally expire after RateLimitWindow. Using SCAN to wipe
        // `ratelimit:feedback_submit:acct:*` would require a second Redis
        // round-trip per test for no test-integrity gain.
        using var scope = Factory.Services.CreateScope();
        var mux = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = mux.GetDatabase();
        await db.KeyDeleteAsync("ratelimit:feedback_submit:ip:unknown");
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private sealed record FeedbackRow(
        Guid Id,
        string Rating,
        string? Text,
        string? Screen,
        Guid? ClusterId,
        Guid? AccountId,
        string? AppVersion,
        string? Platform,
        Guid? SessionId,
        DateTimeOffset SubmittedAt);

    private static async Task<FeedbackRow?> FetchLatestFeedbackRowAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, rating, text, screen, cluster_id, account_id,
                   app_version, platform, session_id, submitted_at
            FROM app_feedback
            ORDER BY submitted_at DESC
            LIMIT 1", conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new FeedbackRow(
            Id: reader.GetGuid(0),
            Rating: reader.GetString(1),
            Text: reader.IsDBNull(2) ? null : reader.GetString(2),
            Screen: reader.IsDBNull(3) ? null : reader.GetString(3),
            ClusterId: reader.IsDBNull(4) ? null : reader.GetGuid(4),
            AccountId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
            AppVersion: reader.IsDBNull(6) ? null : reader.GetString(6),
            Platform: reader.IsDBNull(7) ? null : reader.GetString(7),
            SessionId: reader.IsDBNull(8) ? null : reader.GetGuid(8),
            SubmittedAt: reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static async Task<long> CountFeedbackAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM app_feedback", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is long l ? l : Convert.ToInt64(result);
    }
}

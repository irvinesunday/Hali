using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Feedback;

/// <summary>
/// End-to-end coverage for <c>POST /v1/feedback</c> (issue #156).
/// Proves that valid submissions persist an <c>app_feedback</c> row with the
/// expected field mapping, that invalid bodies are rejected at the controller
/// boundary, and that the endpoint remains anonymous.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class FeedbackIntegrationTests : IntegrationTestBase
{
    public FeedbackIntegrationTests(HaliWebApplicationFactory factory) : base(factory) { }

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

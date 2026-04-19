using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Integration.Marketing;

/// <summary>
/// Integration coverage for <c>POST /v1/marketing/signups</c> and
/// <c>POST /v1/marketing/inquiries</c> (#281).
///
/// Proves:
/// - Valid submissions persist durable PostgreSQL rows with correct field mapping
/// - Invalid payloads are rejected at the controller boundary (400)
/// - The endpoints are accessible without authentication
/// - Over-cap submissions surface the canonical 429 envelope
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class MarketingControllerTests : IntegrationTestBase
{
    public MarketingControllerTests(HaliWebApplicationFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await ClearMarketingRateLimitKeysAsync();
    }

    public override async Task DisposeAsync()
    {
        await ClearMarketingRateLimitKeysAsync();
        await base.DisposeAsync();
    }

    // -----------------------------------------------------------------------
    // #281 — NotifyPersistence_Write_SurvivesRestart
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SignupPersistence_ValidEmail_PersistsDurableRow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var response = await Client.PostAsJsonAsync("/v1/marketing/signups", new
        {
            email = "test@example.com",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT email, submitted_at FROM early_access_signups WHERE email = 'test@example.com'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected a persisted early_access_signups row");
        Assert.Equal("test@example.com", reader.GetString(0));
        Assert.True(reader.GetDateTime(1).ToUniversalTime() >= before,
            "submitted_at should be recent");
    }

    // -----------------------------------------------------------------------
    // #281 — NotifyPersistence_InvalidPayload_ReturnsValidationError
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SignupPersistence_InvalidEmail_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/v1/marketing/signups", new
        {
            email = "not-an-email",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM early_access_signups", conn);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task SignupPersistence_MissingEmail_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/v1/marketing/signups", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // #281 — InquiryPersistence_Write_SurvivesRestart
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InquiryPersistence_ValidPayload_PersistsDurableRow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var response = await Client.PostAsJsonAsync("/v1/marketing/inquiries", new
        {
            name = "Jane Smith",
            organisation = "City of Nairobi",
            role = "Infrastructure Director",
            email = "jane@nairobi.go.ke",
            area = "Westlands",
            category = "water",
            message = "Interested in piloting Hali.",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT name, organisation, role, email, area, category, message, submitted_at
              FROM institution_inquiries WHERE email = 'jane@nairobi.go.ke'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Expected a persisted institution_inquiries row");
        Assert.Equal("Jane Smith", reader.GetString(0));
        Assert.Equal("City of Nairobi", reader.GetString(1));
        Assert.Equal("Infrastructure Director", reader.GetString(2));
        Assert.Equal("jane@nairobi.go.ke", reader.GetString(3));
        Assert.Equal("Westlands", reader.GetString(4));
        Assert.Equal("water", reader.GetString(5));
        Assert.Equal("Interested in piloting Hali.", reader.GetString(6));
        Assert.True(reader.GetDateTime(7).ToUniversalTime() >= before,
            "submitted_at should be recent");
    }

    // -----------------------------------------------------------------------
    // #281 — InquiryPersistence_Read_ReturnsCorrectRecord
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InquiryPersistence_OptionalMessageOmitted_PersistsNullMessage()
    {
        var response = await Client.PostAsJsonAsync("/v1/marketing/inquiries", new
        {
            name = "Bob Jones",
            organisation = "Nairobi Water",
            role = "Manager",
            email = "bob@nw.go.ke",
            area = "CBD",
            category = "electricity",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT message FROM institution_inquiries WHERE email = 'bob@nw.go.ke'", conn);
        var message = await cmd.ExecuteScalarAsync();
        Assert.True(message is DBNull or null, "message should be null when not provided");
    }

    [Fact]
    public async Task InquiryPersistence_InvalidCategory_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/v1/marketing/inquiries", new
        {
            name = "Alice",
            organisation = "Acme",
            role = "Director",
            email = "alice@acme.com",
            area = "Parklands",
            category = "invalid_category",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Signup_Unauthenticated_IsAccepted()
    {
        var response = await Client.PostAsJsonAsync("/v1/marketing/signups", new
        {
            email = "anon@example.com",
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Signup_RateLimit_Returns429AfterCap()
    {
        // The cap is 5/15min per IP. Submit 6 to trigger the 429.
        for (int i = 0; i < 5; i++)
        {
            var r = await Client.PostAsJsonAsync("/v1/marketing/signups", new
            {
                email = $"ratelimit{i}@example.com",
            });
            Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
        }

        var throttled = await Client.PostAsJsonAsync("/v1/marketing/signups", new
        {
            email = "overflow@example.com",
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task ClearMarketingRateLimitKeysAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        await foreach (var key in server.KeysAsync(pattern: "ratelimit:marketing_*"))
        {
            await db.KeyDeleteAsync(key);
        }
    }
}

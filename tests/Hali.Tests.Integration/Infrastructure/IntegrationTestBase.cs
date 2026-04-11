using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Base class for all integration tests.  Inheriting classes receive a clean
/// database before each test and helpers for creating authenticated clients.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    protected HaliWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; private set; } = null!;

    protected IntegrationTestBase(HaliWebApplicationFactory factory)
    {
        Factory = factory;
    }

    // ------------------------------------------------------------------
    // IAsyncLifetime — clean DB before every test
    // ------------------------------------------------------------------

    public virtual async Task InitializeAsync()
    {
        await Factory.CleanTablesAsync();
        await SeedTestLocalityAsync();
        Client = Factory.CreateClient();
    }

    public virtual Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // Auth helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Seeds an OTP challenge directly in the DB with a known code so that
    /// tests can call /v1/auth/verify without triggering real SMS.
    /// </summary>
    protected async Task SeedOtpAsync(string phone, string otp)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var challenge = new OtpChallenge
        {
            Id          = Guid.NewGuid(),
            AuthMethod  = AuthMethod.PhoneOtp,
            Destination = phone,
            OtpHash     = OtpService.HashOtp(otp, phone),
            ExpiresAt   = DateTime.UtcNow.AddMinutes(10),
            CreatedAt   = DateTime.UtcNow,
        };

        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Authenticates via the real HTTP verify flow and returns a server-issued
    /// JWT.  Using a server-issued token guarantees it validates against the
    /// test server's secret/issuer/audience regardless of config overrides.
    /// </summary>
    protected async Task<(Guid AccountId, Guid DeviceId, string Jwt)> SeedVerifiedAccountAsync(
        string phone      = TestConstants.TestPhone,
        string deviceHash = TestConstants.TestDeviceHash)
    {
        const string otp = "999001";
        await SeedOtpAsync(phone, otp);

        var resp = await Client.PostAsJsonAsync("/v1/auth/verify", new
        {
            destination           = phone,
            otp                   = otp,
            deviceFingerprintHash = deviceHash,
            platform              = "android",
            appVersion            = "1.0.0",
        });
        resp.EnsureSuccessStatusCode();

        var body        = await resp.Content.ReadFromJsonAsync<JsonElement>(_json);
        var accessToken = body.GetProperty("accessToken").GetString()!;

        // Retrieve the account and device IDs created by the auth flow
        using var scope  = Factory.Services.CreateScope();
        var authDb = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var account = await authDb.Accounts.FirstAsync(a => a.PhoneE164 == phone);
        var device  = await authDb.Devices.FirstAsync(d => d.DeviceFingerprintHash == deviceHash);

        return (account.Id, device.Id, accessToken);
    }

    /// <summary>
    /// Returns an HttpClient pre-configured with a Bearer token.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(string jwt)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    // ------------------------------------------------------------------
    // Locality seed — needed so the FK on signal_events.locality_id is satisfied
    // ------------------------------------------------------------------

    private static async Task SeedTestLocalityAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO localities (id, country_code, county_name, city_name, ward_name, created_at)
            VALUES (@id, 'KE', 'Nairobi', 'Nairobi', 'Test Ward', now())
            ON CONFLICT (id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("id", FakeLocalityLookupRepository.TestLocalityId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------
    // DB query helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns the ID of the most-recently created cluster in the test DB.
    /// </summary>
    protected static async Task<Guid?> GetLatestClusterIdAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id FROM signal_clusters ORDER BY first_seen_at DESC LIMIT 1", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is Guid id ? id : null;
    }

    /// <summary>
    /// Directly inserts a device into the DB for tests that need a device
    /// without going through the full auth flow.
    /// </summary>
    protected async Task<Guid> SeedDeviceAsync(Guid accountId, string deviceHash)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var device = new Device
        {
            Id                    = Guid.NewGuid(),
            AccountId             = accountId,
            DeviceFingerprintHash = deviceHash,
            CreatedAt             = DateTime.UtcNow,
            LastSeenAt            = DateTime.UtcNow,
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    }
}

using System;
using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Hali.Tests.Integration.DataProtection;

/// <summary>
/// End-to-end coverage for the Data Protection key ring (#243).
/// Exercises the wired <see cref="IDataProtectionProvider"/> against the
/// PostgreSQL-backed key ring (<c>data_protection_keys</c>) and verifies
/// that a protected payload round-trips. Would fail if the DbContext,
/// migration, or DI wiring from #243 were absent or broken.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class DataProtectionIntegrationTests : IntegrationTestBase
{
    public DataProtectionIntegrationTests(HaliWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public void DataProtection_CanProtectAndUnprotect_UsingConfiguredKeyRing()
    {
        IDataProtector protector = Factory.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("hali.integration-test");

        const string plaintext = "test-payload";
        string protectedBlob = protector.Protect(plaintext);
        string unprotected = protector.Unprotect(protectedBlob);

        Assert.Equal(plaintext, unprotected);
        // The ciphertext must not accidentally equal the plaintext —
        // catches the degenerate "null protector" wiring bug.
        Assert.NotEqual(plaintext, protectedBlob);
    }

    [Fact]
    public async Task DataProtection_KeyRing_PersistsCreatedKeysToDatabase()
    {
        // Invoke the key manager directly so the test proves PERSISTENCE
        // rather than cache behaviour: CreateNewKey unconditionally calls
        // IXmlRepository.StoreElement, which with #243's
        // PersistKeysToDbContext wiring writes a new row to
        // data_protection_keys. Without the wiring, the default
        // XmlRepository would write elsewhere and the row count delta
        // would stay at zero.
        //
        // Asserting on a count DELTA (not an absolute count) keeps the
        // test stable across runs: leftover rows from earlier tests do
        // not mask a regression, and we do NOT truncate the table —
        // truncating would desync the Data Protection in-memory key
        // cache from the DB and destabilise other integration tests that
        // rely on the shared IDataProtectionProvider (e.g. the TOTP
        // tests in InstitutionAuthIntegrationTests).
        var keyManager = Factory.Services.GetRequiredService<IKeyManager>();
        long countBefore = await CountKeyRingRowsAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;
        keyManager.CreateNewKey(activationDate: now, expirationDate: now.AddDays(90));

        long countAfter = await CountKeyRingRowsAsync();
        Assert.True(
            countAfter > countBefore,
            $"Expected row count to increase after CreateNewKey. Before: {countBefore}, after: {countAfter}.");
    }

    private static async Task<long> CountKeyRingRowsAsync()
    {
        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM data_protection_keys", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}

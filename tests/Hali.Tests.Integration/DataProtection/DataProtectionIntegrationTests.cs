using System.Threading.Tasks;
using Hali.Tests.Integration.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
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
    public async Task DataProtection_PersistsKeyToDatabase_OnFirstUse()
    {
        // Force the key ring to materialise by calling Protect — the
        // first call writes the seed key to the data_protection_keys
        // table via the EF-backed XmlRepository. Without #243's
        // PersistKeysToDbContext wiring, the ring would live only
        // in memory and this row count would stay at zero.
        IDataProtector protector = Factory.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("hali.integration-test.persistence");

        _ = protector.Protect("warm-up");

        await using var conn = new NpgsqlConnection(TestConstants.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM data_protection_keys", conn);
        long rowCount = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.True(rowCount >= 1, "Expected at least one row in data_protection_keys.");
    }
}

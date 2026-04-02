namespace Hali.Tests.Integration.Infrastructure;

internal static class TestConstants
{
    internal const string ConnectionString =
        "Host=localhost;Port=5432;Database=hali_test;Username=hali;Password=changeme";

    /// <summary>Connects to the postgres maintenance DB to run CREATE DATABASE.</summary>
    internal const string MaintenanceConnectionString =
        "Host=localhost;Port=5432;Database=hali;Username=hali;Password=changeme";

    internal const string JwtSecret   = "integration-test-secret-must-be-at-least-32-chars!!";
    internal const string JwtIssuer   = "hali-test";
    internal const string JwtAudience = "hali-test";

    internal const string TestPhone      = "+254700000001";
    internal const string TestDeviceHash = "test-device-fp-abc123";
}

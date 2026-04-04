using System;

namespace Hali.Tests.Integration.Infrastructure;

internal static class TestConstants
{
    internal static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=hali_test;Username=hali;Password=changeme";

    /// <summary>Connects to the postgres maintenance DB to run CREATE DATABASE.</summary>
    internal static string MaintenanceConnectionString =>
        Environment.GetEnvironmentVariable("TEST_MAINTENANCE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=postgres;Username=hali;Password=changeme";

    internal const string JwtSecret   = "integration-test-secret-must-be-at-least-32-chars!!";
    internal const string JwtIssuer   = "hali-test";
    internal const string JwtAudience = "hali-test";

    internal const string TestPhone      = "+254700000001";
    internal const string TestDeviceHash = "test-device-fp-abc123";
}

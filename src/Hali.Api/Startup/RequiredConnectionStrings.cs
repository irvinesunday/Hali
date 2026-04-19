using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Hali.Api.Startup;

/// <summary>
/// Fail-fast validation for required database connection strings.
/// Called at startup in Production to ensure all <see cref="Hali.Infrastructure.Data.HaliDataSources"/>
/// connection strings are present before any database operation is attempted.
/// </summary>
public static class RequiredConnectionStrings
{
    // Names must match the connection string keys used when building HaliDataSources
    // in ServiceCollectionExtensions.cs. Keep in sync.
    private static readonly IReadOnlyList<string> RequiredNames = new[]
    {
        "Auth",
        "Signals",
        "Clusters",
        "Participation",
        "Advisories",
        "Notifications",
        "Feedback",
    };

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if any required connection
    /// string is missing in Production. No-op in non-Production environments.
    /// </summary>
    public static void EnsureAllPresent(IConfiguration configuration, IHostEnvironment env)
    {
        if (!env.IsProduction())
        {
            return;
        }

        foreach (string name in RequiredNames)
        {
            string? value = configuration.GetConnectionString(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Required connection string '{name}' is missing. " +
                    $"Set ConnectionStrings:{name} in configuration.");
            }
        }
    }
}

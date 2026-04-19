using System;
using System.Collections.Generic;
using Hali.Infrastructure.Auth;
using Hali.Infrastructure.Redis;
using Hali.Infrastructure.Signals;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Security;

/// <summary>
/// Unit tests for startup options validators.
/// Each validator must enforce non-empty secrets in Production and
/// tolerate empty values in non-Production so local development works
/// without real third-party credentials.
/// </summary>
public class OptionsValidatorTests
{
    private static IHostEnvironment MakeEnv(bool isProduction)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(isProduction ? Environments.Production : Environments.Development);
        return env;
    }

    // ------------------------------------------------------------------ //
    // AfricasTalkingOptionsValidator
    // ------------------------------------------------------------------ //

    [Fact]
    public void AfricasTalkingOptionsValidator_Production_FailsWhenApiKeyMissing()
    {
        AfricasTalkingOptionsValidator validator = new AfricasTalkingOptionsValidator(MakeEnv(true));
        AfricasTalkingOptions options = new AfricasTalkingOptions
        {
            ApiKey = string.Empty,
            Username = "sandbox",
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ApiKey", result.FailureMessage, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AfricasTalkingOptionsValidator_NonProduction_AllowsEmptyApiKey()
    {
        AfricasTalkingOptionsValidator validator = new AfricasTalkingOptionsValidator(MakeEnv(false));
        AfricasTalkingOptions options = new AfricasTalkingOptions
        {
            ApiKey = string.Empty,
            Username = string.Empty,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    // ------------------------------------------------------------------ //
    // AnthropicOptionsValidator
    // ------------------------------------------------------------------ //

    [Fact]
    public void AnthropicOptionsValidator_Production_FailsWhenApiKeyMissing()
    {
        AnthropicOptionsValidator validator = new AnthropicOptionsValidator(MakeEnv(true));
        AnthropicOptions options = new AnthropicOptions
        {
            ApiKey = string.Empty,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ApiKey", result.FailureMessage, System.StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ //
    // RedisOptionsValidator
    // ------------------------------------------------------------------ //

    [Fact]
    public void RedisOptionsValidator_Production_FailsWhenUrlMissing()
    {
        RedisOptionsValidator validator = new RedisOptionsValidator(MakeEnv(true));
        RedisOptions options = new RedisOptions
        {
            Url = string.Empty,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Url", result.FailureMessage, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedisOptionsValidator_NonProduction_AllowsFallbackUrl()
    {
        RedisOptionsValidator validator = new RedisOptionsValidator(MakeEnv(false));
        RedisOptions options = new RedisOptions
        {
            Url = string.Empty,
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    // ------------------------------------------------------------------ //
    // RequiredConnectionStrings
    // ------------------------------------------------------------------ //

    [Fact]
    public void RequiredConnectionStrings_Production_ThrowsOnMissingName()
    {
        // Build config without the "Signals" connection string to trigger failure.
        IConfiguration config =
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Auth"] = "Host=localhost",
                    // Signals deliberately omitted
                    ["ConnectionStrings:Clusters"] = "Host=localhost",
                    ["ConnectionStrings:Participation"] = "Host=localhost",
                    ["ConnectionStrings:Advisories"] = "Host=localhost",
                    ["ConnectionStrings:Notifications"] = "Host=localhost",
                    ["ConnectionStrings:Feedback"] = "Host=localhost",
                })
                .Build();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Hali.Api.Startup.RequiredConnectionStrings.EnsureAllPresent(config, MakeEnv(true)));

        Assert.Contains("Signals", ex.Message, StringComparison.Ordinal);
    }
}

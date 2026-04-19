using System;
using System.Collections.Generic;
using System.Net;
using Hali.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Security;

/// <summary>
/// Unit tests for <see cref="ForwardedHeadersConfigurator"/>.
/// Tests target the internal parsing helpers to verify that known proxies and
/// networks are parsed correctly from configuration and that invalid values or
/// misconfigured Production environments throw at startup with a clear message.
/// </summary>
public class ForwardedHeadersConfiguratorTests
{
    private static IHostEnvironment MakeEnv(bool isProduction)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(isProduction ? Environments.Production : Environments.Development);
        return env;
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void ForwardedHeaders_ConfigLoader_ParsesKnownProxies_FromConfiguration()
    {
        IPAddress ip1 = ForwardedHeadersConfigurator.ParseProxyIp("10.0.0.1");
        IPAddress ip2 = ForwardedHeadersConfigurator.ParseProxyIp("10.0.0.2");

        Assert.Equal("10.0.0.1", ip1.ToString());
        Assert.Equal("10.0.0.2", ip2.ToString());
    }

    [Fact]
    public void ForwardedHeaders_ConfigLoader_ParsesKnownNetworksCidr_FromConfiguration()
    {
        (IPAddress networkAddress, int prefixLength) = ForwardedHeadersConfigurator.ParseCidrParts("10.0.0.0/8");

        Assert.Equal("10.0.0.0", networkAddress.ToString());
        Assert.Equal(8, prefixLength);
    }

    [Fact]
    public void ForwardedHeaders_ConfigLoader_ThrowsOnInvalidProxyIp()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ForwardedHeadersConfigurator.ParseProxyIp("not-an-ip"));

        Assert.Contains("not-an-ip", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeaders_ConfigLoader_ThrowsOnInvalidCidr()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ForwardedHeadersConfigurator.ParseCidrParts("bad-cidr"));

        Assert.Contains("bad-cidr", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ForwardedHeaders_Production_RequireProxyConfig_ThrowsWhenEmpty()
    {
        IConfiguration config = BuildConfig(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:RequireProxyConfig"] = "true",
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ForwardedHeadersConfigurator.EnforceRequireProxyConfig(
                config, MakeEnv(isProduction: true), proxyCount: 0, networkCount: 0));

        Assert.Contains("RequireProxyConfig", ex.Message, StringComparison.Ordinal);
    }
}

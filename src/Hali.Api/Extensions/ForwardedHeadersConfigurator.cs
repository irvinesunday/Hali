using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Extensions;

/// <summary>
/// Configures <see cref="ForwardedHeadersOptions"/> from application configuration.
/// Reads <c>ForwardedHeaders:KnownProxies</c> (array of IP address strings) and
/// <c>ForwardedHeaders:KnownNetworks</c> (array of CIDR strings e.g. "10.0.0.0/8").
/// </summary>
public static class ForwardedHeadersConfigurator
{
    /// <summary>
    /// Applies production-configurable ForwardedHeaders trust rules to <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options instance to mutate.</param>
    /// <param name="config">Application configuration.</param>
    /// <param name="env">Host environment (used for production fail-fast check).</param>
    /// <param name="startupLogger">Optional logger; count of trusted entries is logged (never the IPs).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an IP address or CIDR cannot be parsed, or when
    /// <c>ForwardedHeaders:RequireProxyConfig</c> is <c>true</c> in Production
    /// and both <c>KnownProxies</c> and <c>KnownNetworks</c> are empty.
    /// </exception>
    public static void ConfigureForwardedHeaders(
        ForwardedHeadersOptions options,
        IConfiguration config,
        IHostEnvironment env,
        ILogger? startupLogger)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        string[]? proxyStrings = config.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>();
        string[]? networkStrings = config.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>();

        int proxyCount = 0;
        int networkCount = 0;

        if (proxyStrings is { Length: > 0 })
        {
            foreach (string raw in proxyStrings)
            {
                IPAddress ip = ParseProxyIp(raw);
                options.KnownProxies.Add(ip);
                proxyCount++;
            }
        }

        if (networkStrings is { Length: > 0 })
        {
            foreach (string raw in networkStrings)
            {
                Microsoft.AspNetCore.HttpOverrides.IPNetwork network = ParseNetworkCidr(raw);
                options.KnownNetworks.Add(network);
                networkCount++;
            }
        }

        EnforceRequireProxyConfig(config, env, proxyCount, networkCount);

        // Log the count of trusted entries only — never log the IPs themselves to
        // avoid leaking infrastructure topology into log aggregation systems.
        startupLogger?.LogInformation(
            "[forwarded-headers] Trusted proxy config loaded: {ProxyCount} known proxies, {NetworkCount} known networks.",
            proxyCount,
            networkCount);
    }

    /// <summary>
    /// Parses a single proxy IP address string.
    /// Throws <see cref="InvalidOperationException"/> with the bad value in the message on failure.
    /// Exposed as <c>internal</c> for unit testing without a <see cref="ForwardedHeadersOptions"/> instance.
    /// </summary>
    internal static IPAddress ParseProxyIp(string raw)
    {
        if (!IPAddress.TryParse(raw, out IPAddress? ip))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownProxies contains an invalid IP address: '{raw}'. " +
                "Provide a valid IPv4 or IPv6 address.");
        }

        return ip;
    }

    /// <summary>
    /// Parses a CIDR string in the form "address/prefixLength".
    /// Throws <see cref="InvalidOperationException"/> with the bad value in the message on failure.
    /// Exposed as <c>internal</c> for unit testing without a <see cref="ForwardedHeadersOptions"/> instance.
    /// </summary>
    internal static (IPAddress NetworkAddress, int PrefixLength) ParseCidrParts(string cidr)
    {
        int slashIndex = cidr.LastIndexOf('/');
        if (slashIndex < 1 || slashIndex >= cidr.Length - 1)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownNetworks contains an invalid CIDR: '{cidr}'. " +
                "Expected format: '<network-address>/<prefix-length>' (e.g. '10.0.0.0/8').");
        }

        string addressPart = cidr[..slashIndex];
        string prefixPart = cidr[(slashIndex + 1)..];

        if (!IPAddress.TryParse(addressPart, out IPAddress? networkAddress))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownNetworks contains an invalid CIDR: '{cidr}'. " +
                $"The network address '{addressPart}' is not a valid IP address.");
        }

        if (!int.TryParse(prefixPart, out int prefixLength))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownNetworks contains an invalid CIDR: '{cidr}'. " +
                $"The prefix length '{prefixPart}' is not a valid integer.");
        }

        int maxPrefix = networkAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
        if (prefixLength < 0 || prefixLength > maxPrefix)
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownNetworks contains an invalid CIDR: '{cidr}'. " +
                $"Prefix length '{prefixLength}' is out of range [0, {maxPrefix}].");
        }

        return (networkAddress, prefixLength);
    }

    /// <summary>
    /// Enforces the <c>RequireProxyConfig</c> fail-fast rule in Production.
    /// Exposed as <c>internal</c> for unit testing.
    /// </summary>
    internal static void EnforceRequireProxyConfig(
        IConfiguration config,
        IHostEnvironment env,
        int proxyCount,
        int networkCount)
    {
        bool requireConfig = config.GetValue("ForwardedHeaders:RequireProxyConfig", defaultValue: false);
        if (requireConfig && env.IsProduction() && proxyCount == 0 && networkCount == 0)
        {
            throw new InvalidOperationException(
                "ForwardedHeaders:RequireProxyConfig is true in Production but neither " +
                "ForwardedHeaders:KnownProxies nor ForwardedHeaders:KnownNetworks are configured. " +
                "Add your load-balancer IP addresses or CIDR ranges to the configuration.");
        }
    }

    private static Microsoft.AspNetCore.HttpOverrides.IPNetwork ParseNetworkCidr(string cidr)
    {
        (IPAddress networkAddress, int prefixLength) = ParseCidrParts(cidr);
        return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkAddress, prefixLength);
    }
}

using System;
using System.Diagnostics.Metrics;
using Hali.Api.Observability;
using Hali.Application.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Disposable wrapper around a test-owned <see cref="ApiMetrics"/> and the
/// <see cref="ServiceProvider"/> that hosts its <see cref="IMeterFactory"/>.
///
/// Using a real <c>IMeterFactory</c> (instead of a hand-rolled <c>Meter</c>)
/// exercises the same registration path the production container uses, so
/// <c>MeterListener</c> attaches to the same meter OTel would export in
/// production. Disposing the scope tears down both the <see cref="Metrics"/>
/// instance (and its underlying <see cref="Meter"/>) and the
/// <see cref="ServiceProvider"/> so repeated test construction does not leak
/// disposables across the test suite.
/// </summary>
internal sealed class TestMetricsScope : IDisposable
{
    private readonly ServiceProvider _provider;
    public ApiMetrics Metrics { get; }

    internal TestMetricsScope(ServiceProvider provider, ApiMetrics metrics)
    {
        _provider = provider;
        Metrics = metrics;
    }

    public void Dispose()
    {
        Metrics.Dispose();
        _provider.Dispose();
    }
}

/// <summary>
/// Factory for <see cref="TestMetricsScope"/>. Callers own the returned
/// scope and must dispose it (via <c>using</c> or a fixture).
/// </summary>
internal static class TestMetrics
{
    public static TestMetricsScope Create()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        return new TestMetricsScope(provider, new ApiMetrics(factory));
    }
}

/// <summary>
/// Disposable wrapper around a test-owned <see cref="HomeMetrics"/> and the
/// <see cref="ServiceProvider"/> that hosts its <see cref="IMeterFactory"/>.
/// Mirrors <see cref="TestMetricsScope"/> for the home-feed meter so each
/// test gets an isolated <see cref="System.Diagnostics.Metrics.Meter"/>
/// instance and <see cref="System.Diagnostics.Metrics.MeterListener"/>
/// observations stay scoped to that test.
/// </summary>
internal sealed class TestHomeMetricsScope : IDisposable
{
    private readonly ServiceProvider _provider;
    public HomeMetrics Metrics { get; }

    internal TestHomeMetricsScope(ServiceProvider provider, HomeMetrics metrics)
    {
        _provider = provider;
        Metrics = metrics;
    }

    public void Dispose()
    {
        Metrics.Dispose();
        _provider.Dispose();
    }
}

/// <summary>
/// Factory for <see cref="TestHomeMetricsScope"/>. Callers own the returned
/// scope and must dispose it (via <c>using</c> or a fixture).
/// </summary>
internal static class TestHomeMetrics
{
    public static TestHomeMetricsScope Create()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        return new TestHomeMetricsScope(provider, new HomeMetrics(factory));
    }
}

/// <summary>
/// Disposable wrapper around a test-owned <see cref="SignalsMetrics"/> and the
/// <see cref="ServiceProvider"/> that hosts its <see cref="IMeterFactory"/>.
/// Mirrors <see cref="TestHomeMetricsScope"/> for the signals meter so each
/// test gets an isolated <see cref="System.Diagnostics.Metrics.Meter"/>
/// instance and <see cref="System.Diagnostics.Metrics.MeterListener"/>
/// observations stay scoped to that test.
/// </summary>
internal sealed class TestSignalsMetricsScope : IDisposable
{
    private readonly ServiceProvider _provider;
    public SignalsMetrics Metrics { get; }

    internal TestSignalsMetricsScope(ServiceProvider provider, SignalsMetrics metrics)
    {
        _provider = provider;
        Metrics = metrics;
    }

    public void Dispose()
    {
        Metrics.Dispose();
        _provider.Dispose();
    }
}

/// <summary>
/// Factory for <see cref="TestSignalsMetricsScope"/>. Callers own the returned
/// scope and must dispose it (via <c>using</c> or a fixture).
/// </summary>
internal static class TestSignalsMetrics
{
    public static TestSignalsMetricsScope Create()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        return new TestSignalsMetricsScope(provider, new SignalsMetrics(factory));
    }
}

/// <summary>
/// Disposable wrapper around a test-owned <see cref="ClustersMetrics"/> and the
/// <see cref="ServiceProvider"/> that hosts its <see cref="IMeterFactory"/>.
/// Mirrors <see cref="TestSignalsMetricsScope"/> for the clusters meter so each
/// test gets an isolated <see cref="System.Diagnostics.Metrics.Meter"/>
/// instance and <see cref="System.Diagnostics.Metrics.MeterListener"/>
/// observations stay scoped to that test.
/// </summary>
internal sealed class TestClustersMetricsScope : IDisposable
{
    private readonly ServiceProvider _provider;
    public ClustersMetrics Metrics { get; }

    internal TestClustersMetricsScope(ServiceProvider provider, ClustersMetrics metrics)
    {
        _provider = provider;
        Metrics = metrics;
    }

    public void Dispose()
    {
        Metrics.Dispose();
        _provider.Dispose();
    }
}

/// <summary>
/// Factory for <see cref="TestClustersMetricsScope"/>. Callers own the returned
/// scope and must dispose it (via <c>using</c> or a fixture).
/// </summary>
internal static class TestClustersMetrics
{
    public static TestClustersMetricsScope Create()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        return new TestClustersMetricsScope(provider, new ClustersMetrics(factory));
    }
}

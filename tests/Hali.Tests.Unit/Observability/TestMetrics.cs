using System;
using System.Diagnostics.Metrics;
using Hali.Api.Observability;
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

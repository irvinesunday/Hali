using System.Diagnostics.Metrics;
using Hali.Api.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Shared helper for constructing an <see cref="ApiMetrics"/> backed by a
/// real <see cref="IMeterFactory"/>. Using the real factory (instead of a
/// hand-rolled <c>Meter</c>) exercises the same registration path the
/// production container uses, so <c>MeterListener</c> attaches to the same
/// meter OTel would export in production.
/// </summary>
internal static class TestMetrics
{
    public static ApiMetrics Create()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IMeterFactory>();
        return new ApiMetrics(factory);
    }
}

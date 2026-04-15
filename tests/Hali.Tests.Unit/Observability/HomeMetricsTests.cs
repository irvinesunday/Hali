using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Api.Controllers;
using Hali.Api.Observability;
using Hali.Application.Errors;
using Hali.Application.Home;
using Hali.Application.Notifications;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Hali.Tests.Unit.Observability;

/// <summary>
/// Verifies that <c>HomeController</c> emits the home-feed observability
/// instruments owned by <see cref="HomeMetrics"/>:
/// <list type="bullet">
///   <item><description>the <c>home_feed_request_duration_seconds</c>
///     histogram fires exactly once per request, on every code path,
///     with the intended bounded tag set;</description></item>
///   <item><description>the <c>home_feed_cache_hits_total</c> /
///     <c>home_feed_cache_misses_total</c> counters increment exactly once
///     per cache-eligible request and never on the cache-bypass paths.</description></item>
/// </list>
///
/// Each test owns an isolated <see cref="HomeMetrics"/> via
/// <see cref="TestHomeMetrics"/> so the <see cref="MeterListener"/> only
/// observes measurements from that test's meter — keeping the suite
/// parallel-safe.
/// </summary>
public class HomeMetricsTests
{
    private sealed record DoubleMeasurement(double Value, Dictionary<string, object?> Tags);
    private sealed record LongMeasurement(long Value, Dictionary<string, object?> Tags);

    /// <summary>
    /// Captures every measurement emitted by the three home-feed instruments
    /// for the lifetime of the listener. The listener filters by reference
    /// equality on the test-owned <see cref="HomeMetrics"/> instruments so
    /// other meters in-process cannot pollute the recorded measurements.
    /// </summary>
    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        public List<DoubleMeasurement> DurationMeasurements { get; } = new();
        public List<LongMeasurement> CacheHitMeasurements { get; } = new();
        public List<LongMeasurement> CacheMissMeasurements { get; } = new();

        public MetricCapture(HomeMetrics metrics)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument, metrics.HomeFeedRequestDuration)
                    || ReferenceEquals(instrument, metrics.HomeFeedCacheHitsTotal)
                    || ReferenceEquals(instrument, metrics.HomeFeedCacheMissesTotal))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (ReferenceEquals(instrument, metrics.HomeFeedRequestDuration))
                {
                    DurationMeasurements.Add(new DoubleMeasurement(measurement, ToDict(tags)));
                }
            });

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                var dict = ToDict(tags);
                if (ReferenceEquals(instrument, metrics.HomeFeedCacheHitsTotal))
                {
                    CacheHitMeasurements.Add(new LongMeasurement(measurement, dict));
                }
                else if (ReferenceEquals(instrument, metrics.HomeFeedCacheMissesTotal))
                {
                    CacheMissMeasurements.Add(new LongMeasurement(measurement, dict));
                }
            });

            _listener.Start();
        }

        private static Dictionary<string, object?> ToDict(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            foreach (var tag in tags)
            {
                dict[tag.Key] = tag.Value;
            }
            return dict;
        }

        public void Dispose() => _listener.Dispose();
    }

    private static readonly Guid TestLocalityId = Guid.Parse("c0c0c0c0-1111-2222-3333-444455556666");
    private static readonly Guid TestAccountId = Guid.Parse("a0a0a0a0-1111-2222-3333-444455556666");

    private static HomeController CreateController(
        HomeMetrics metrics,
        IHomeFeedQueryService feedQuery,
        IFollowService follows,
        IDatabase redis,
        bool authenticated)
    {
        var controller = new HomeController(
            feedQuery,
            follows,
            redis,
            Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions()),
            metrics,
            NullLogger<HomeController>.Instance);

        var context = new DefaultHttpContext();
        if (authenticated)
        {
            var claims = new[]
            {
                new System.Security.Claims.Claim(
                    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                    TestAccountId.ToString())
            };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "test");
            context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        }
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static (IHomeFeedQueryService feedQuery, IFollowService follows, IDatabase redis) BuildEmptyDeps(
        bool followsLocality = true)
    {
        var feedQuery = Substitute.For<IHomeFeedQueryService>();
        var follows = Substitute.For<IFollowService>();
        var redis = Substitute.For<IDatabase>();

        follows.GetFollowedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(followsLocality
                ? new List<Follow> { new() { LocalityId = TestLocalityId } }
                : new List<Follow>());

        feedQuery.GetActiveByLocalitiesPagedAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<bool?>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        feedQuery.GetAllActivePagedAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(),
                Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
        feedQuery.GetOfficialPostsByLocalitiesAsync(
                Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<OfficialPostResponseDto>)Array.Empty<OfficialPostResponseDto>());

        redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(RedisValue.Null);

        return (feedQuery, follows, redis);
    }

    // ── Histogram tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetHome_AuthenticatedFallback_EmitsHistogramOnce_WithFallbackTag()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        await controller.GetHome(null, null, null, CancellationToken.None);

        var m = Assert.Single(capture.DurationMeasurements);
        Assert.True(m.Value >= 0);
        Assert.Equal("true", m.Tags[HomeMetrics.TagAuthenticated]);
        Assert.Equal(HomeMetrics.LocalityScopeFallback, m.Tags[HomeMetrics.TagLocalityScope]);
    }

    [Fact]
    public async Task GetHome_AnonymousExplicitLocality_EmitsHistogramOnce_WithExplicitTag()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps(followsLocality: false);
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: false);

        await controller.GetHome(null, null, TestLocalityId, CancellationToken.None);

        var m = Assert.Single(capture.DurationMeasurements);
        Assert.Equal("false", m.Tags[HomeMetrics.TagAuthenticated]);
        Assert.Equal(HomeMetrics.LocalityScopeExplicit, m.Tags[HomeMetrics.TagLocalityScope]);
    }

    [Fact]
    public async Task GetHome_AnonymousNoLocality_EmitsHistogramOnce_WithGuestEmptyTag()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps(followsLocality: false);
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: false);

        await controller.GetHome(null, null, null, CancellationToken.None);

        var m = Assert.Single(capture.DurationMeasurements);
        Assert.Equal("false", m.Tags[HomeMetrics.TagAuthenticated]);
        Assert.Equal(HomeMetrics.LocalityScopeGuestEmpty, m.Tags[HomeMetrics.TagLocalityScope]);
    }

    [Fact]
    public async Task GetHome_TwoConsecutiveRequests_EmitsHistogramTwice()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        await controller.GetHome(null, null, null, CancellationToken.None);
        await controller.GetHome(null, null, null, CancellationToken.None);

        Assert.Equal(2, capture.DurationMeasurements.Count);
    }

    [Fact]
    public async Task GetHome_InvalidSection_StillEmitsHistogramOnce_OnExceptionPath()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        await Assert.ThrowsAsync<ValidationException>(
            () => controller.GetHome("not_a_real_section", null, null, CancellationToken.None));

        // Histogram emits once even when the handler throws, so failed-request
        // latency stays observable. Cache counters do not fire on the section
        // path because section requests bypass the Redis cache by design.
        var m = Assert.Single(capture.DurationMeasurements);
        Assert.Equal("true", m.Tags[HomeMetrics.TagAuthenticated]);
        Assert.Equal(HomeMetrics.LocalityScopeFallback, m.Tags[HomeMetrics.TagLocalityScope]);
        Assert.Empty(capture.CacheHitMeasurements);
        Assert.Empty(capture.CacheMissMeasurements);
    }

    // ── Cache hit/miss counter tests ─────────────────────────────────────────

    [Fact]
    public async Task GetHome_CacheMiss_IncrementsMissCounterExactlyOnce_AndNoHit()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        await controller.GetHome(null, null, null, CancellationToken.None);

        var miss = Assert.Single(capture.CacheMissMeasurements);
        Assert.Equal(1L, miss.Value);
        Assert.Empty(capture.CacheHitMeasurements);
    }

    [Fact]
    public async Task GetHome_CacheHit_IncrementsHitCounterExactlyOnce_AndNoMiss()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("{}"));
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        await controller.GetHome(null, null, null, CancellationToken.None);

        var hit = Assert.Single(capture.CacheHitMeasurements);
        Assert.Equal(1L, hit.Value);
        Assert.Empty(capture.CacheMissMeasurements);
    }

    [Fact]
    public async Task GetHome_AnonymousNoLocality_CacheBypass_NoCacheCounters()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps(followsLocality: false);
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: false);

        await controller.GetHome(null, null, null, CancellationToken.None);

        // Cache is only consulted when there is at least one resolved
        // locality. Anonymous + no localityId means the cache path is
        // bypassed entirely; neither counter should fire.
        Assert.Empty(capture.CacheHitMeasurements);
        Assert.Empty(capture.CacheMissMeasurements);
        // Histogram still fires for the bypass path.
        Assert.Single(capture.DurationMeasurements);
    }

    [Fact]
    public async Task GetHome_PaginatedSection_BypassesCache_NoCacheCountersFire()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        await controller.GetHome("active_now", null, null, CancellationToken.None);

        // Section requests skip the cache entirely; counters must not fire,
        // but the histogram still observes the request.
        Assert.Empty(capture.CacheHitMeasurements);
        Assert.Empty(capture.CacheMissMeasurements);
        Assert.Single(capture.DurationMeasurements);
    }

    [Fact]
    public async Task GetHome_CursorPaginated_BypassesCache_NoCacheCountersFire()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        // Encode a non-null cursor — any base64 of ticks works.
        var cursor = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks.ToString()));

        await controller.GetHome(null, cursor, null, CancellationToken.None);

        Assert.Empty(capture.CacheHitMeasurements);
        Assert.Empty(capture.CacheMissMeasurements);
        Assert.Single(capture.DurationMeasurements);
    }

    [Fact]
    public async Task GetHome_CacheHitAndMiss_AreMutuallyExclusivePerRequest()
    {
        using var scope = TestHomeMetrics.Create();
        using var capture = new MetricCapture(scope.Metrics);
        var (feedQuery, follows, redis) = BuildEmptyDeps();
        var controller = CreateController(scope.Metrics, feedQuery, follows, redis, authenticated: true);

        // First request: cache miss.
        await controller.GetHome(null, null, null, CancellationToken.None);

        // Second request: simulate a cache hit by flipping the mock.
        redis.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(new RedisValue("{}"));
        await controller.GetHome(null, null, null, CancellationToken.None);

        // Exactly one miss (request 1) + exactly one hit (request 2) — the
        // implementation never counts both for the same request.
        Assert.Single(capture.CacheMissMeasurements);
        Assert.Single(capture.CacheHitMeasurements);
        Assert.Equal(2, capture.DurationMeasurements.Count);
    }
}

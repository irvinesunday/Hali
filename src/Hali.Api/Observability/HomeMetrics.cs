using System;
using System.Diagnostics.Metrics;

namespace Hali.Api.Observability;

/// <summary>
/// Hosts the <c>Hali.Home</c> <see cref="Meter"/> and the instruments emitted
/// from <c>HomeController</c>. The home feed is the single hottest read
/// endpoint for the citizen mobile app, so this meter is the operational
/// signal Phase 1 pilot readiness depends on:
/// <list type="bullet">
///   <item><description><c>home_feed_request_duration_seconds</c> — request
///     latency histogram covering the controller handler from dispatch through
///     response build (cache and assembly paths both included).</description></item>
///   <item><description><c>home_feed_cache_hits_total</c> /
///     <c>home_feed_cache_misses_total</c> — Redis cache outcome counters for
///     the cache-eligible path (no cursor, at least one locality).</description></item>
/// </list>
///
/// The meter is registered on the OpenTelemetry meter provider in
/// <c>Program.cs</c> under the name <see cref="MeterName"/>. When
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is unset the meter and its instruments
/// still exist in-process (zero-cost, non-exported) and no behaviour regresses.
///
/// Tag values are bounded by static catalogs (no localityId, accountId, path,
/// or request-derived strings) so cardinality stays controlled at scale —
/// see the per-instrument summaries below.
/// </summary>
public sealed class HomeMetrics : IDisposable
{
    /// <summary>
    /// Name of the home-feed <see cref="Meter"/>. Mirrored by
    /// <c>AddMeter(HomeMetrics.MeterName)</c> on the OpenTelemetry meter
    /// provider so instruments export through the existing OTLP transport.
    /// </summary>
    public const string MeterName = "Hali.Home";

    /// <summary>Histogram name — request latency in seconds.</summary>
    public const string HomeFeedRequestDurationName = "home_feed_request_duration_seconds";

    /// <summary>Counter name — cache-eligible requests served from Redis.</summary>
    public const string HomeFeedCacheHitsTotalName = "home_feed_cache_hits_total";

    /// <summary>Counter name — cache-eligible requests that missed Redis.</summary>
    public const string HomeFeedCacheMissesTotalName = "home_feed_cache_misses_total";

    /// <summary>
    /// Tag key carrying authentication posture. Values: <c>"true"</c> or
    /// <c>"false"</c> (string, not bool, so OTLP exporters render the tag
    /// consistently across backends).
    /// </summary>
    public const string TagAuthenticated = "authenticated";

    /// <summary>
    /// Tag key carrying locality-resolution outcome. Values:
    /// <list type="bullet">
    ///   <item><description><c>"explicit"</c> — caller passed <c>?localityId</c>.</description></item>
    ///   <item><description><c>"fallback"</c> — authenticated caller with a
    ///     non-empty followed-localities set. Also used on the exception path
    ///     when the follow lookup throws for an authenticated caller without
    ///     an explicit locality, so dependency outages in that lookup stay
    ///     observable as fallback-mode failures rather than being silently
    ///     rebucketed as guest traffic.</description></item>
    ///   <item><description><c>"guest_empty"</c> — no localities were
    ///     resolved. Anonymous callers without an explicit locality, and
    ///     authenticated callers whose follow set resolved empty, both bucket
    ///     here.</description></item>
    /// </list>
    /// </summary>
    public const string TagLocalityScope = "locality_scope";

    // Locality-scope tag values — re-exported as constants so call sites and
    // tests never duplicate string literals. Keep these in lockstep with the
    // ObservabilityEvents.HomeLocalityScope* log event suffixes.
    public const string LocalityScopeExplicit = "explicit";
    public const string LocalityScopeFallback = "fallback";
    public const string LocalityScopeGuestEmpty = "guest_empty";

    private readonly Meter _meter;

    /// <summary>
    /// <c>home_feed_request_duration_seconds</c> — total controller handler
    /// duration recorded once per request (success and failure). The recorded
    /// span starts at controller dispatch and ends after the response is built
    /// (both cache-hit and cache-miss/assembly paths are included), giving a
    /// single operationally meaningful "what the caller waited for" signal
    /// without coupling the metric to any internal sub-step.
    ///
    /// Tags (bounded — 2 × 3 = 6 combinations max):
    /// <list type="bullet">
    ///   <item><description><see cref="TagAuthenticated"/> —
    ///     <c>"true"</c> or <c>"false"</c>.</description></item>
    ///   <item><description><see cref="TagLocalityScope"/> —
    ///     <c>"explicit" | "fallback" | "guest_empty"</c>.</description></item>
    /// </list>
    /// No request-derived value (path, method, locality id, account id,
    /// correlation id, cursor, section name) is ever attached.
    ///
    /// Cancellation policy: emission is skipped on client-disconnect
    /// unwinds — specifically, an <see cref="OperationCanceledException"/>
    /// propagating while <c>HttpContext.RequestAborted.IsCancellationRequested</c>
    /// is <c>true</c>. This mirrors the <c>api_exceptions_total</c>
    /// cancellation carve-out in <c>ExceptionHandlingMiddleware</c> so
    /// downstream alerts and dashboards can correlate the two instruments
    /// under one cancellation policy. Non-aborted
    /// <see cref="OperationCanceledException"/> paths (e.g. server-side
    /// internal timeouts) still emit as ordinary failure-path latency observations.
    /// </summary>
    public Histogram<double> HomeFeedRequestDuration { get; }

    /// <summary>
    /// <c>home_feed_cache_hits_total</c> — incremented exactly once per
    /// cache-eligible request that was served from Redis. Cache eligibility
    /// matches <c>HomeController</c>: no cursor and at least one resolved
    /// locality. Untagged: a single counter aligned with the alert query
    /// <c>rate(home_feed_cache_hits_total) /
    ///       (rate(home_feed_cache_hits_total) + rate(home_feed_cache_misses_total))</c>.
    /// </summary>
    public Counter<long> HomeFeedCacheHitsTotal { get; }

    /// <summary>
    /// <c>home_feed_cache_misses_total</c> — incremented exactly once per
    /// cache-eligible request that did not find a cached entry. Counterpart
    /// of <see cref="HomeFeedCacheHitsTotal"/>; together they cover every
    /// cache-eligible request exactly once.
    /// </summary>
    public Counter<long> HomeFeedCacheMissesTotal { get; }

    public HomeMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        HomeFeedRequestDuration = _meter.CreateHistogram<double>(
            name: HomeFeedRequestDurationName,
            unit: "s",
            description: "End-to-end home feed request duration as observed by HomeController.");

        HomeFeedCacheHitsTotal = _meter.CreateCounter<long>(
            name: HomeFeedCacheHitsTotalName,
            unit: "{request}",
            description: "Cache-eligible home feed requests served from the Redis cache.");

        HomeFeedCacheMissesTotal = _meter.CreateCounter<long>(
            name: HomeFeedCacheMissesTotalName,
            unit: "{request}",
            description: "Cache-eligible home feed requests that missed the Redis cache.");
    }

    public void Dispose() => _meter.Dispose();
}

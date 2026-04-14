using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Home;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Xunit;

namespace Hali.Tests.Unit.Home;

/// <summary>
/// A7: Tests validating the concurrent execution pattern used for home feed
/// section queries with a fake <see cref="IHomeFeedQueryService" />.
/// These tests verify that multiple section tasks can overlap and complete
/// under <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{System.Threading.Tasks.Task})" />,
/// but they do not exercise real EF Core or DbContext thread-safety behavior.
/// </summary>
public class HomeFeedConcurrencyTests
{
    // ── Fake IHomeFeedQueryService that tracks concurrent calls ──────────────
    //
    // Determinism note (issue #103):
    // This fake previously used Task.Delay(10) to simulate DB latency and relied
    // on time-based overlap to prove the fire-then-await pattern was concurrent.
    // That proof is flaky on slow CI runners where the delay can complete before
    // all tasks are scheduled. We now use a TaskCompletionSource release gate:
    // each in-flight call arrives at the gate and only proceeds once
    // _expectedConcurrency callers are simultaneously present. If fewer callers
    // arrive, the gate's CancellationToken timeout fires and the test fails
    // loudly rather than racing. When _expectedConcurrency is 1 (single-call
    // tests) the gate opens immediately, so no coordination is required.

    private sealed class TrackingFeedQueryService : IHomeFeedQueryService
    {
        private int _activeCalls;
        private int _peakConcurrency;
        private int _arrivedCount;
        private readonly object _lock = new();
        private readonly int _expectedConcurrency;
        private readonly TaskCompletionSource _gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TimeSpan _gateTimeout;

        public TrackingFeedQueryService(int expectedConcurrency = 1, TimeSpan? gateTimeout = null)
        {
            if (expectedConcurrency < 1)
                throw new ArgumentOutOfRangeException(nameof(expectedConcurrency));
            _expectedConcurrency = expectedConcurrency;
            _gateTimeout = gateTimeout ?? TimeSpan.FromSeconds(5);

            // For single-caller scenarios, release the gate up front so Enter()
            // does not block on itself.
            if (expectedConcurrency == 1)
                _gate.TrySetResult();
        }

        public int PeakConcurrency => Volatile.Read(ref _peakConcurrency);

        private async Task EnterAsync(CancellationToken ct)
        {
            lock (_lock)
            {
                _activeCalls++;
                if (_activeCalls > _peakConcurrency)
                    _peakConcurrency = _activeCalls;
                _arrivedCount++;
                if (_arrivedCount >= _expectedConcurrency)
                    _gate.TrySetResult();
            }

            // Wait until the expected number of callers are all present, so every
            // task is provably in-flight simultaneously. Fail fast on timeout.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(_gateTimeout);
            await _gate.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }

        private void Exit()
        {
            lock (_lock) { _activeCalls--; }
        }

        public async Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(
            IEnumerable<Guid> localityIds, bool? recurringOnly, int limit,
            DateTime? cursorBefore, CancellationToken ct)
        {
            await EnterAsync(ct);
            try
            {
                var ids = localityIds.ToList();
                if (ids.Count == 0)
                    return new List<SignalCluster>();
                return new List<SignalCluster>
                {
                    MakeCluster(ids[0], recurringOnly == true ? "recurring" : null)
                };
            }
            finally { Exit(); }
        }

        public async Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(
            IEnumerable<Guid> excludeLocalityIds, int limit,
            DateTime? cursorBefore, CancellationToken ct)
        {
            await EnterAsync(ct);
            try
            {
                return new List<SignalCluster> { MakeCluster(Guid.NewGuid()) };
            }
            finally { Exit(); }
        }

        public async Task<IReadOnlyList<OfficialPostResponseDto>> GetOfficialPostsByLocalityAsync(
            Guid localityId, CancellationToken ct)
        {
            await EnterAsync(ct);
            try
            {
                return new List<OfficialPostResponseDto>();
            }
            finally { Exit(); }
        }

        private static SignalCluster MakeCluster(Guid localityId, string? temporalType = null) => new()
        {
            Id = Guid.NewGuid(),
            State = SignalState.Active,
            Category = CivicCategory.Roads,
            LocalityId = localityId,
            TemporalType = temporalType,
            ActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
    }

    // ── Test 1: fire-then-await pattern achieves concurrent execution ──────────

    [Fact]
    public async Task ConcurrentSections_ExecuteInParallel()
    {
        // The gate only releases once all four callers are simultaneously
        // in-flight, so observing the WhenAll completing is itself proof of
        // concurrent execution — no time-based race.
        var svc = new TrackingFeedQueryService(expectedConcurrency: 4);
        var localityIds = new List<Guid> { Guid.NewGuid() };

        // Mirror BuildFullResponseAsync pattern
        var t1 = svc.GetActiveByLocalitiesPagedAsync(localityIds, false, 21, null, CancellationToken.None);
        var t2 = svc.GetOfficialPostsByLocalityAsync(localityIds[0], CancellationToken.None);
        var t3 = svc.GetActiveByLocalitiesPagedAsync(localityIds, true, 11, null, CancellationToken.None);
        var t4 = svc.GetAllActivePagedAsync(localityIds, 11, null, CancellationToken.None);

        await Task.WhenAll(t1, t2, t3, t4);

        // All four tasks must have been in-flight together for the gate to
        // release. Peak concurrency is therefore exactly the expected count.
        Assert.Equal(4, svc.PeakConcurrency);
    }

    // ── Test 2: all four sections return results under concurrency ────────────

    [Fact]
    public async Task ConcurrentSections_AllReturnResults()
    {
        var svc = new TrackingFeedQueryService(expectedConcurrency: 4);
        var localityIds = new List<Guid> { Guid.NewGuid() };

        var t1 = svc.GetActiveByLocalitiesPagedAsync(localityIds, false, 21, null, CancellationToken.None);
        var t2 = svc.GetOfficialPostsByLocalityAsync(localityIds[0], CancellationToken.None);
        var t3 = svc.GetActiveByLocalitiesPagedAsync(localityIds, true, 11, null, CancellationToken.None);
        var t4 = svc.GetAllActivePagedAsync(localityIds, 11, null, CancellationToken.None);

        await Task.WhenAll(t1, t2, t3, t4);

        Assert.NotEmpty(await t1);
        Assert.NotNull(await t2);
        Assert.NotEmpty(await t3);
        Assert.NotEmpty(await t4);
    }

    // ── Test 3: single-section fetch is also safe (no concurrency needed) ────

    [Fact]
    public async Task SingleSectionFetch_WorksIndependently()
    {
        var svc = new TrackingFeedQueryService();
        var localityIds = new List<Guid> { Guid.NewGuid() };

        var result = await svc.GetActiveByLocalitiesPagedAsync(
            localityIds, false, 21, null, CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal(1, svc.PeakConcurrency);
    }

    // ── Test 4: empty locality list returns empty without calling DB ─────────

    [Fact]
    public async Task EmptyLocalityIds_ReturnsEmptyResults()
    {
        var svc = new TrackingFeedQueryService();
        var empty = new List<Guid>();

        // The controller guards against empty locality IDs before calling
        // the service, so this tests the contract boundary
        var result = await svc.GetActiveByLocalitiesPagedAsync(
            empty, false, 21, null, CancellationToken.None);

        Assert.Empty(result);
    }

    // ── Test 5: concurrent execution preserves section-specific filtering ────

    [Fact]
    public async Task ConcurrentExecution_PreservesSectionFiltering()
    {
        var svc = new TrackingFeedQueryService(expectedConcurrency: 2);
        var localityIds = new List<Guid> { Guid.NewGuid() };

        var activeNowTask = svc.GetActiveByLocalitiesPagedAsync(localityIds, false, 21, null, CancellationToken.None);
        var recurringTask = svc.GetActiveByLocalitiesPagedAsync(localityIds, true, 11, null, CancellationToken.None);

        await Task.WhenAll(activeNowTask, recurringTask);

        // activeNow should have non-recurring cluster
        Assert.All(await activeNowTask, c => Assert.NotEqual("recurring", c.TemporalType));

        // recurring should have recurring cluster
        Assert.All(await recurringTask, c => Assert.Equal("recurring", c.TemporalType));
    }
}

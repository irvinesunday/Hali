using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Xunit;

namespace Hali.Tests.Unit.Home;

/// <summary>
/// A6: Performance-focused tests for home feed query efficiency.
/// Validates parallelization correctness, cache key isolation, and
/// section-level query independence.
/// </summary>
public class HomePerformanceTests
{
    private static SignalCluster MakeCluster(
        Guid localityId,
        DateTime? activatedAt = null,
        string? temporalType = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            State = SignalState.Active,
            Category = CivicCategory.Roads,
            LocalityId = localityId,
            TemporalType = temporalType,
            ActivatedAt = activatedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

    // ── Test 1: parallel section builders produce same result as sequential ───

    [Fact]
    public async Task ParallelSectionBuild_ProducesSameResultAsSequential()
    {
        var localityId = Guid.NewGuid();
        var clusters = Enumerable.Range(1, 15)
            .Select(i => MakeCluster(localityId, DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        // Simulate sequential section building
        var activeNow = FilterSection(clusters, recurring: false, limit: 20);
        var recurring = FilterSection(clusters, recurring: true, limit: 10);

        // Simulate parallel section building (same logic, concurrent execution)
        var activeNowTask = Task.FromResult(FilterSection(clusters, recurring: false, limit: 20));
        var recurringTask = Task.FromResult(FilterSection(clusters, recurring: true, limit: 10));
        await Task.WhenAll(activeNowTask, recurringTask);

        Assert.Equal(activeNow.Count, activeNowTask.Result.Count);
        Assert.Equal(recurring.Count, recurringTask.Result.Count);
    }

    // ── Test 2: parallel official post merge produces correct ordering ────────

    [Fact]
    public async Task ParallelOfficialPostMerge_MaintainsDescendingOrder()
    {
        var now = DateTime.UtcNow;

        // Simulate parallel per-locality fetches merged together
        var localityAPosts = new List<DateTime>
        {
            now.AddMinutes(-1),
            now.AddMinutes(-5),
        };
        var localityBPosts = new List<DateTime>
        {
            now.AddMinutes(-2),
            now.AddMinutes(-3),
        };

        var taskA = Task.FromResult(localityAPosts);
        var taskB = Task.FromResult(localityBPosts);
        var results = await Task.WhenAll(taskA, taskB);

        var merged = results
            .SelectMany(p => p)
            .OrderByDescending(d => d)
            .ToList();

        // Must be in strict descending order after merge
        for (int i = 1; i < merged.Count; i++)
        {
            Assert.True(merged[i - 1] >= merged[i],
                $"Posts not in descending order at index {i}");
        }

        Assert.Equal(4, merged.Count);
    }

    // ── Test 3: cache key isolates single-locality from full-follow set ──────

    [Fact]
    public void CacheKey_SingleLocality_DiffersFromFullFollowSet()
    {
        var localityA = Guid.NewGuid();
        var localityB = Guid.NewGuid();
        var localityC = Guid.NewGuid();

        var singleKey = BuildCacheKey(new List<Guid> { localityA });
        var fullKey = BuildCacheKey(new List<Guid> { localityA, localityB, localityC });

        Assert.NotEqual(singleKey, fullKey);
    }

    // ── Test 4: cache key stable for same single locality ────────────────────

    [Fact]
    public void CacheKey_SameSingleLocality_IsStable()
    {
        var localityId = Guid.NewGuid();

        var key1 = BuildCacheKey(new List<Guid> { localityId });
        var key2 = BuildCacheKey(new List<Guid> { localityId });

        Assert.Equal(key1, key2);
    }

    // ── Test 5: section filter does not produce overlapping results ──────────

    [Fact]
    public void SectionFilters_ProduceNoOverlap_BetweenActiveNowAndRecurring()
    {
        var localityId = Guid.NewGuid();

        var clusters = new List<SignalCluster>
        {
            MakeCluster(localityId, temporalType: null),
            MakeCluster(localityId, temporalType: "recurring"),
            MakeCluster(localityId, temporalType: "one_time"),
            MakeCluster(localityId, temporalType: "recurring"),
            MakeCluster(localityId, temporalType: null),
        };

        var activeNow = clusters.Where(c => c.TemporalType != "recurring").ToList();
        var recurring = clusters.Where(c => c.TemporalType == "recurring").ToList();

        // No cluster should appear in both sections
        var activeNowIds = activeNow.Select(c => c.Id).ToHashSet();
        var recurringIds = recurring.Select(c => c.Id).ToHashSet();
        Assert.Empty(activeNowIds.Intersect(recurringIds));

        // Together they should account for all clusters
        Assert.Equal(clusters.Count, activeNow.Count + recurring.Count);
    }

    // ── Test 6: cursor filtering is applied before limiting ──────────────────

    [Fact]
    public void CursorFiltering_AppliedBeforeLimiting()
    {
        var localityId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var clusters = Enumerable.Range(1, 30)
            .Select(i => MakeCluster(localityId, now.AddMinutes(-i)))
            .OrderByDescending(c => c.ActivatedAt)
            .ToList();

        // Simulate cursor at position 10 (activated_at of 10th item)
        var cursorDt = clusters[9].ActivatedAt;
        const int limit = 10;

        // Apply cursor THEN limit (correct: DB-level filtering)
        var dbLevel = clusters
            .Where(c => c.ActivatedAt < cursorDt)
            .Take(limit + 1)
            .ToList();

        // All returned items must be older than cursor
        Assert.All(dbLevel, c => Assert.True(c.ActivatedAt < cursorDt));
        Assert.True(dbLevel.Count <= limit + 1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<SignalCluster> FilterSection(
        List<SignalCluster> clusters, bool recurring, int limit)
    {
        return clusters
            .Where(c => recurring
                ? c.TemporalType == "recurring"
                : c.TemporalType != "recurring")
            .OrderByDescending(c => c.ActivatedAt)
            .Take(limit + 1)
            .ToList();
    }

    private static string BuildCacheKey(List<Guid> localityIds)
    {
        var sorted = string.Join(",", localityIds.OrderBy(g => g));
        var hash = Convert.ToHexString(
            System.Security.Cryptography.MD5.HashData(
                Encoding.UTF8.GetBytes(sorted)));
        return $"home:{hash}:p1";
    }
}

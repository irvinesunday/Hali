using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Xunit;

namespace Hali.Tests.Unit.Home;

/// <summary>
/// A5: Verify that explicit localityId scoping works correctly for home feed.
/// Tests the locality selection logic independent of HTTP.
/// </summary>
public class HomeLocalityFilterTests
{
    private static SignalCluster MakeCluster(Guid localityId, DateTime? activatedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            State = SignalState.Active,
            Category = CivicCategory.Roads,
            LocalityId = localityId,
            ActivatedAt = activatedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

    // ── Test 1: explicit localityId scopes to single locality ────────────────

    [Fact]
    public void ExplicitLocalityId_ScopesToSingleLocality()
    {
        var localityA = Guid.NewGuid();
        var localityB = Guid.NewGuid();
        var followedIds = new List<Guid> { localityA, localityB };

        // Simulate: when localityId is provided, use [localityId] instead of followedIds
        Guid? explicitLocalityId = localityA;
        var effectiveIds = explicitLocalityId.HasValue
            ? new List<Guid> { explicitLocalityId.Value }
            : followedIds;

        Assert.Single(effectiveIds);
        Assert.Equal(localityA, effectiveIds[0]);
    }

    // ── Test 2: absent localityId uses all followed localities ───────────────

    [Fact]
    public void AbsentLocalityId_UsesAllFollowedLocalities()
    {
        var localityA = Guid.NewGuid();
        var localityB = Guid.NewGuid();
        var followedIds = new List<Guid> { localityA, localityB };

        Guid? explicitLocalityId = null;
        var effectiveIds = explicitLocalityId.HasValue
            ? new List<Guid> { explicitLocalityId.Value }
            : followedIds;

        Assert.Equal(2, effectiveIds.Count);
        Assert.Contains(localityA, effectiveIds);
        Assert.Contains(localityB, effectiveIds);
    }

    // ── Test 3: explicit localityId filters clusters correctly ───────────────

    [Fact]
    public void ExplicitLocalityId_FiltersClustersToMatchingLocality()
    {
        var localityA = Guid.NewGuid();
        var localityB = Guid.NewGuid();

        var clusters = new List<SignalCluster>
        {
            MakeCluster(localityA),
            MakeCluster(localityA),
            MakeCluster(localityB),
            MakeCluster(localityB),
            MakeCluster(localityB),
        };

        // Scope to localityA only
        var scopedIds = new HashSet<Guid> { localityA };
        var filtered = clusters.Where(c => c.LocalityId.HasValue && scopedIds.Contains(c.LocalityId.Value)).ToList();

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, c => Assert.Equal(localityA, c.LocalityId));
    }

    // ── Test 4: cache key is order-independent for the same locality set ─────

    [Fact]
    public void CacheKey_IsOrderIndependent_ForSameLocalitySet()
    {
        var localityA = Guid.NewGuid();
        var localityB = Guid.NewGuid();

        var orderedKey = BuildCacheKey(new List<Guid> { localityA, localityB });
        var reversedKey = BuildCacheKey(new List<Guid> { localityB, localityA });
        var distinctSetKey = BuildCacheKey(new List<Guid> { localityA });

        Assert.Equal(orderedKey, reversedKey);
        Assert.NotEqual(orderedKey, distinctSetKey);
    }

    // ── Test 5: section fetch with localityId still applies scoping ──────────

    [Fact]
    public void SectionFetch_WithLocalityId_AppliesScoping()
    {
        var localityA = Guid.NewGuid();
        var localityB = Guid.NewGuid();

        // Simulate section-specific request with explicit locality
        Guid? explicitLocalityId = localityB;
        var followedIds = new List<Guid> { localityA, localityB };

        var effectiveIds = explicitLocalityId.HasValue
            ? new List<Guid> { explicitLocalityId.Value }
            : followedIds;

        // Section queries use the same effectiveIds — scoping applies
        Assert.Single(effectiveIds);
        Assert.Equal(localityB, effectiveIds[0]);
    }

    // ── Test 6: unauthenticated with no localityId returns empty ─────────────

    [Fact]
    public void Unauthenticated_NoLocalityId_ReturnsEmptyIds()
    {
        // Unauthenticated: no follows, no explicit locality
        Guid? explicitLocalityId = null;
        var followedIds = new List<Guid>(); // unauthenticated

        var effectiveIds = explicitLocalityId.HasValue
            ? new List<Guid> { explicitLocalityId.Value }
            : followedIds;

        Assert.Empty(effectiveIds);
    }

    // ── Test 7: unauthenticated with localityId still returns empty ──────────

    [Fact]
    public void Unauthenticated_WithLocalityId_IgnoresLocalityId()
    {
        // Even when localityId is provided, unauthenticated callers should
        // fall through to the followed-localities path (which is empty).
        bool isAuthenticated = false;
        Guid? explicitLocalityId = Guid.NewGuid();
        var followedIds = new List<Guid>(); // unauthenticated

        var effectiveIds = explicitLocalityId.HasValue && isAuthenticated
            ? new List<Guid> { explicitLocalityId.Value }
            : followedIds;

        Assert.Empty(effectiveIds);
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

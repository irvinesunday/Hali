using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Xunit;

namespace Hali.Tests.Unit.Home;

/// <summary>
/// B-7: Home feed pagination — per-section limits and cursor-based pagination.
/// Tests verify repository paging logic (limit+1 probe, cursor encoding) independent of HTTP.
/// </summary>
public class HomeFeedPaginationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SignalCluster MakeCluster(DateTime? activatedAt = null, string? temporalType = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            State = SignalState.Active,
            Category = CivicCategory.Roads,
            LocalityId = Guid.NewGuid(),
            TemporalType = temporalType,
            ActivatedAt = activatedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };

    private static string? EncodeCursor(DateTime? dt)
    {
        if (dt is null) return null;
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(dt.Value.Ticks.ToString()));
    }

    private static DateTime? DecodeCursor(string? cursor)
    {
        if (cursor is null) return null;
        var ticks = long.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    // ── Test 1: active_now section — returns max 20 ───────────────────────────

    [Fact]
    public void GetHome_ActiveNow_ReturnsMaxTwenty()
    {
        // Arrange — 25 active non-recurring clusters
        var clusters = Enumerable.Range(1, 25)
            .Select(i => MakeCluster(DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        // Simulate the controller logic: take limit+1=21 from repo, return only 20
        const int limit = 20;
        var raw = clusters.Take(limit + 1).ToList(); // simulate repo returning 21

        bool hasMore = raw.Count > limit;
        var items = raw.Take(limit).ToList();
        string? nextCursor = hasMore ? EncodeCursor(raw[limit - 1].ActivatedAt) : null;

        Assert.Equal(20, items.Count);
        Assert.True(hasMore);
        Assert.NotNull(nextCursor);
    }

    // ── Test 2: cursor-based pagination returns next page ─────────────────────

    [Fact]
    public void GetHome_WithCursor_ReturnsNextPage()
    {
        // Arrange — 30 clusters ordered newest-first
        var base_ = DateTime.UtcNow;
        var clusters = Enumerable.Range(1, 30)
            .Select(i => MakeCluster(base_.AddMinutes(-i)))
            .OrderByDescending(c => c.ActivatedAt)
            .ToList();

        const int limit = 20;

        // First page
        var page1 = clusters.Take(limit + 1).ToList();
        Assert.True(page1.Count > limit);
        var cursor = EncodeCursor(page1[limit - 1].ActivatedAt);

        // Decode cursor and filter
        var cursorDt = DecodeCursor(cursor);
        Assert.NotNull(cursorDt);

        // Second page — clusters older than cursor
        var page2 = clusters
            .Where(c => c.ActivatedAt < cursorDt)
            .Take(limit + 1)
            .ToList();

        Assert.Equal(10, page2.Count); // 30 - 20 = 10 remaining
        Assert.False(page2.Count > limit); // no third page
    }

    // ── Test 3: empty section returns null next_cursor ────────────────────────

    [Fact]
    public void GetHome_EmptySection_ReturnsEmptyWithNullCursor()
    {
        // Arrange — no clusters
        var clusters = new List<SignalCluster>();

        const int limit = 20;
        var raw = clusters.Take(limit + 1).ToList();

        bool hasMore = raw.Count > limit;
        var items = raw.Take(limit).ToList();
        string? nextCursor = hasMore ? EncodeCursor(raw[limit - 1].ActivatedAt) : null;

        Assert.Empty(items);
        Assert.False(hasMore);
        Assert.Null(nextCursor);
    }

    // ── Test 4: recurring section — respects limit of 10 ─────────────────────

    [Fact]
    public void GetHome_RecurringSection_ReturnsMaxTen()
    {
        // Arrange — 15 recurring clusters
        var clusters = Enumerable.Range(1, 15)
            .Select(i => MakeCluster(DateTime.UtcNow.AddMinutes(-i), temporalType: "recurring"))
            .ToList();

        const int limit = 10;
        var raw = clusters.Take(limit + 1).ToList();

        bool hasMore = raw.Count > limit;
        var items = raw.Take(limit).ToList();
        string? nextCursor = hasMore ? EncodeCursor(raw[limit - 1].ActivatedAt) : null;

        Assert.Equal(10, items.Count);
        Assert.True(hasMore);
        Assert.NotNull(nextCursor);
    }

    // ── Test 5: other_active_signals — exactly at limit returns null cursor ───

    [Fact]
    public void GetHome_OtherActiveSignals_ExactlyAtLimit_ReturnsNullCursor()
    {
        // Arrange — exactly 10 clusters (= limit for other_active_signals)
        var clusters = Enumerable.Range(1, 10)
            .Select(i => MakeCluster(DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        const int limit = 10;
        var raw = clusters.Take(limit + 1).ToList(); // 10 returned (not limit+1=11)

        bool hasMore = raw.Count > limit;
        var items = raw.Take(limit).ToList();
        string? nextCursor = hasMore ? EncodeCursor(raw[limit - 1].ActivatedAt) : null;

        Assert.Equal(10, items.Count);
        Assert.False(hasMore);
        Assert.Null(nextCursor);
    }
}

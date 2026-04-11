using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Application.Signals;
using Hali.Domain.Entities.Notifications;
using Xunit;

namespace Hali.Tests.Unit.Notifications;

public class FollowServiceTests
{
    private sealed class FakeFollowRepo : IFollowRepository
    {
        private readonly List<Follow> _store = new();

        public Task<IReadOnlyList<Follow>> GetByAccountAsync(Guid accountId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Follow>)_store.Where(f => f.AccountId == accountId).ToList());

        public Task<IReadOnlyList<Follow>> GetByLocalityAsync(Guid localityId, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Follow>)_store.Where(f => f.LocalityId == localityId).ToList());

        public Task<int> CountByAccountAsync(Guid accountId, CancellationToken ct = default)
            => Task.FromResult(_store.Count(f => f.AccountId == accountId));

        public Task ReplaceFollowsAsync(Guid accountId, IEnumerable<FollowEntry> entries, CancellationToken ct = default)
        {
            _store.RemoveAll(f => f.AccountId == accountId);
            foreach (var entry in entries)
                _store.Add(new Follow { Id = Guid.NewGuid(), AccountId = accountId, LocalityId = entry.LocalityId, DisplayLabel = entry.DisplayLabel, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalityLookup : ILocalityLookupRepository
    {
        public Dictionary<Guid, LocalitySummary> Store { get; } = new();
        public bool AutoPopulate { get; set; } = true;

        public Task<IReadOnlyDictionary<Guid, LocalitySummary>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            if (AutoPopulate)
            {
                foreach (var id in ids)
                {
                    if (!Store.ContainsKey(id))
                        Store[id] = new LocalitySummary(id, "Ward " + id.ToString().Substring(0, 4), "Nairobi", "Nairobi");
                }
            }
            var dict = ids
                .Where(i => Store.ContainsKey(i))
                .ToDictionary(i => i, i => Store[i]);
            return Task.FromResult((IReadOnlyDictionary<Guid, LocalitySummary>)dict);
        }

        public Task<LocalitySummary?> FindByPointAsync(double latitude, double longitude, CancellationToken ct = default)
            => Task.FromResult<LocalitySummary?>(null);
    }

    private static FollowService NewService(IFollowRepository? repo = null, FakeLocalityLookup? lookup = null)
        => new FollowService(repo ?? new FakeFollowRepo(), lookup ?? new FakeLocalityLookup());

    [Fact]
    public async Task SetFollowed_WithFiveLocalities_Succeeds()
    {
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        await svc.SetFollowedAsync(accountId, ids.Select(i => new FollowEntry(i, null)));
        var result = await svc.GetFollowedAsync(accountId);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task SetFollowed_WithSixLocalities_ThrowsMaxExceeded()
    {
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetFollowedAsync(accountId, ids.Select(i => new FollowEntry(i, null))));

        Assert.Equal("MAX_FOLLOWED_LOCALITIES_EXCEEDED", ex.Message);
    }

    [Fact]
    public async Task SetFollowed_DedupesInput_BeforeEnforcing()
    {
        // 6 items but all the same ID → deduped to 1 → should succeed
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var sameId = Guid.NewGuid();
        var ids = Enumerable.Repeat(sameId, 6).ToList();

        await svc.SetFollowedAsync(accountId, ids.Select(i => new FollowEntry(i, null))); // no exception
        var result = await svc.GetFollowedAsync(accountId);
        Assert.Single(result);
    }

    [Fact]
    public async Task SetFollowed_ReplacesExistingFollows()
    {
        var repo = new FakeFollowRepo();
        var svc = NewService(repo);
        var accountId = Guid.NewGuid();

        var first = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        await svc.SetFollowedAsync(accountId, first.Select(i => new FollowEntry(i, null)));

        var second = Enumerable.Range(0, 2).Select(_ => Guid.NewGuid()).ToList();
        await svc.SetFollowedAsync(accountId, second.Select(i => new FollowEntry(i, null)));

        var result = await svc.GetFollowedAsync(accountId);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Contains(f.LocalityId, second));
    }

    // -----------------------------------------------------------------------
    // GetFollowedWithDetailsAsync — joins follows + locality lookup
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetFollowedWithDetails_ReturnsDisplayLabel_WhenPresent()
    {
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var localityId = Guid.NewGuid();

        await svc.SetFollowedAsync(accountId, new[] { new FollowEntry(localityId, "South B") });

        var result = await svc.GetFollowedWithDetailsAsync(accountId);

        var dto = Assert.Single(result);
        Assert.Equal(localityId, dto.LocalityId);
        Assert.Equal("South B", dto.DisplayLabel);
        Assert.False(string.IsNullOrEmpty(dto.WardName));
    }

    [Fact]
    public async Task GetFollowedWithDetails_ReturnsNullDisplayLabel_WhenNotProvided()
    {
        // UI is responsible for falling back to wardName when DisplayLabel
        // is null. The service must surface the null faithfully — not
        // synthesize a label.
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var localityId = Guid.NewGuid();

        await svc.SetFollowedAsync(accountId, new[] { new FollowEntry(localityId, null) });

        var result = await svc.GetFollowedWithDetailsAsync(accountId);

        var dto = Assert.Single(result);
        Assert.Null(dto.DisplayLabel);
        Assert.False(string.IsNullOrEmpty(dto.WardName));
    }

    [Fact]
    public async Task GetFollowedWithDetails_DropsFollows_WhenLocalityLookupMissing()
    {
        // If the Signals DB no longer has a row for a followed locality,
        // the service must NOT return a DTO with an empty wardName — it
        // would violate the API contract. The follow is dropped instead.
        var lookup = new FakeLocalityLookup { AutoPopulate = false };
        var repo = new FakeFollowRepo();
        var svc = NewService(repo, lookup);

        var accountId = Guid.NewGuid();
        var present = Guid.NewGuid();
        var missing = Guid.NewGuid();
        lookup.Store[present] = new LocalitySummary(present, "Makadara Ward", "Nairobi", "Nairobi");

        await svc.SetFollowedAsync(accountId, new[]
        {
            new FollowEntry(present, "South B"),
            new FollowEntry(missing, "Ghost Estate"),
        });

        var result = await svc.GetFollowedWithDetailsAsync(accountId);

        var dto = Assert.Single(result);
        Assert.Equal(present, dto.LocalityId);
        Assert.Equal("Makadara Ward", dto.WardName);
    }

    // -----------------------------------------------------------------------
    // SetFollowedAsync — dedupe behavior preserves non-null DisplayLabel
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetFollowed_DedupePrefersNonNullDisplayLabel()
    {
        // Two entries for the same localityId — the second carries the
        // label. The dedupe must keep the labelled entry, not silently
        // drop it because it appears later.
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var localityId = Guid.NewGuid();

        await svc.SetFollowedAsync(accountId, new[]
        {
            new FollowEntry(localityId, null),
            new FollowEntry(localityId, "South B"),
        });

        var follows = await svc.GetFollowedAsync(accountId);
        var follow = Assert.Single(follows);
        Assert.Equal("South B", follow.DisplayLabel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SetFollowed_DedupeTreatsBlankDisplayLabelAsMissing(string blank)
    {
        // SetFollowedAsync uses IsNullOrWhiteSpace to detect a "missing"
        // label, so an empty or whitespace-only DisplayLabel must not
        // clobber a real labelled entry that arrives in the same call.
        var svc = NewService();
        var accountId = Guid.NewGuid();
        var localityId = Guid.NewGuid();

        await svc.SetFollowedAsync(accountId, new[]
        {
            new FollowEntry(localityId, blank),
            new FollowEntry(localityId, "South B"),
        });

        var follows = await svc.GetFollowedAsync(accountId);
        var follow = Assert.Single(follows);
        Assert.Equal("South B", follow.DisplayLabel);
    }
}

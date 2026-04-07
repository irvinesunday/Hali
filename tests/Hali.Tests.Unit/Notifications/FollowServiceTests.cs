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
        public Task<IReadOnlyDictionary<Guid, LocalitySummary>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyDictionary<Guid, LocalitySummary>)
                ids.ToDictionary(id => id, id => new LocalitySummary(id, "Ward " + id.ToString().Substring(0, 4), "Nairobi", "Nairobi")));

        public Task<LocalitySummary?> FindByPointAsync(double latitude, double longitude, CancellationToken ct = default)
            => Task.FromResult<LocalitySummary?>(null);
    }

    private static FollowService NewService(IFollowRepository? repo = null)
        => new FollowService(repo ?? new FakeFollowRepo(), new FakeLocalityLookup());

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
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Application.Notifications;
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

        public Task ReplaceFollowsAsync(Guid accountId, IEnumerable<Guid> localityIds, CancellationToken ct = default)
        {
            _store.RemoveAll(f => f.AccountId == accountId);
            foreach (var id in localityIds)
                _store.Add(new Follow { Id = Guid.NewGuid(), AccountId = accountId, LocalityId = id, CreatedAt = DateTime.UtcNow });
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SetFollowed_WithFiveLocalities_Succeeds()
    {
        var svc = new FollowService(new FakeFollowRepo());
        var accountId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        await svc.SetFollowedAsync(accountId, ids);
        var result = await svc.GetFollowedAsync(accountId);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task SetFollowed_WithSixLocalities_ThrowsMaxExceeded()
    {
        var svc = new FollowService(new FakeFollowRepo());
        var accountId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => svc.SetFollowedAsync(accountId, ids));

        Assert.Equal("validation.max_followed_localities_exceeded", ex.Code);
    }

    [Fact]
    public async Task SetFollowed_DedupesInput_BeforeEnforcing()
    {
        // 6 items but all the same ID → deduped to 1 → should succeed
        var svc = new FollowService(new FakeFollowRepo());
        var accountId = Guid.NewGuid();
        var sameId = Guid.NewGuid();
        var ids = Enumerable.Repeat(sameId, 6).ToList();

        await svc.SetFollowedAsync(accountId, ids); // no exception
        var result = await svc.GetFollowedAsync(accountId);
        Assert.Single(result);
    }

    [Fact]
    public async Task SetFollowed_ReplacesExistingFollows()
    {
        var repo = new FakeFollowRepo();
        var svc = new FollowService(repo);
        var accountId = Guid.NewGuid();

        var first = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        await svc.SetFollowedAsync(accountId, first);

        var second = Enumerable.Range(0, 2).Select(_ => Guid.NewGuid()).ToList();
        await svc.SetFollowedAsync(accountId, second);

        var result = await svc.GetFollowedAsync(accountId);
        Assert.Equal(2, result.Count);
        Assert.All(result, f => Assert.Contains(f.LocalityId, second));
    }
}

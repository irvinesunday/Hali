using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Domain.Entities.Notifications;
using Hali.Infrastructure.Data.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Notifications;

public class FollowRepository : IFollowRepository
{
    private readonly NotificationsDbContext _db;

    public FollowRepository(NotificationsDbContext db)
    {
        _db = db;
    }

    public Task<IReadOnlyList<Follow>> GetByAccountAsync(Guid accountId, CancellationToken ct = default)
        => _db.Follows
            .Where(f => f.AccountId == accountId)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Follow>)t.Result, ct);

    public Task<IReadOnlyList<Follow>> GetByLocalityAsync(Guid localityId, CancellationToken ct = default)
        => _db.Follows
            .Where(f => f.LocalityId == localityId)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<Follow>)t.Result, ct);

    public Task<int> CountByAccountAsync(Guid accountId, CancellationToken ct = default)
        => _db.Follows.CountAsync(f => f.AccountId == accountId, ct);

    public async Task ReplaceFollowsAsync(Guid accountId, IEnumerable<Guid> localityIds, CancellationToken ct = default)
    {
        var existing = await _db.Follows.Where(f => f.AccountId == accountId).ToListAsync(ct);
        _db.Follows.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var localityId in localityIds)
        {
            _db.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                LocalityId = localityId,
                CreatedAt = now
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}

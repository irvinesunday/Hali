using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Auth;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Auth;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Auth;

public class InstitutionRepository : IInstitutionRepository
{
    private readonly AuthDbContext _authDb;
    private readonly AdvisoriesDbContext _advisoriesDb;

    public InstitutionRepository(AuthDbContext authDb, AdvisoriesDbContext advisoriesDb)
    {
        _authDb = authDb;
        _advisoriesDb = advisoriesDb;
    }

    public async Task<Institution> CreateInstitutionAsync(Institution institution, CancellationToken ct = default)
    {
        _advisoriesDb.Institutions.Add(institution);
        await _advisoriesDb.SaveChangesAsync(ct);
        return institution;
    }

    public async Task SaveInviteAsync(InstitutionInvite invite, CancellationToken ct = default)
    {
        _authDb.InstitutionInvites.Add(invite);
        await _authDb.SaveChangesAsync(ct);
    }

    public Task<InstitutionInvite?> FindInviteByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return _authDb.InstitutionInvites
            .FirstOrDefaultAsync(i => i.InviteTokenHash == tokenHash, ct);
    }

    public async Task AcceptInviteAsync(InstitutionInvite invite, DateTime acceptedAt, CancellationToken ct = default)
    {
        invite.AcceptedAt = acceptedAt;
        await _authDb.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetAccountIdsByInstitutionAsync(Guid institutionId, CancellationToken ct = default)
    {
        return await _authDb.Accounts
            .Where(a => a.InstitutionId == institutionId)
            .Select(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task BlockAccountsAsync(IEnumerable<Guid> accountIds, CancellationToken ct = default)
    {
        var ids = accountIds.ToList();
        var accounts = await _authDb.Accounts
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(ct);
        foreach (var account in accounts)
            account.IsBlocked = true;
        await _authDb.SaveChangesAsync(ct);
    }

    public async Task RevokeRefreshTokensByAccountIdsAsync(IEnumerable<Guid> accountIds, DateTime revokedAt, CancellationToken ct = default)
    {
        var ids = accountIds.ToList();
        var tokens = await _authDb.RefreshTokens
            .Where(t => ids.Contains(t.AccountId) && t.RevokedAt == null && t.ExpiresAt > revokedAt)
            .ToListAsync(ct);
        foreach (var token in tokens)
            token.RevokedAt = revokedAt;
        await _authDb.SaveChangesAsync(ct);
    }
}

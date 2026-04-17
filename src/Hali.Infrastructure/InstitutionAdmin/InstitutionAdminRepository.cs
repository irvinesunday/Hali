using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.InstitutionAdmin;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data;
using Hali.Infrastructure.Data.Advisories;
using Hali.Infrastructure.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hali.Infrastructure.InstitutionAdmin;

public sealed class InstitutionAdminRepository : IInstitutionAdminRepository
{
    private readonly AuthDbContext _authDb;
    private readonly AdvisoriesDbContext _advisoriesDb;
    private readonly HaliDataSources _dataSources;

    public InstitutionAdminRepository(
        AuthDbContext authDb,
        AdvisoriesDbContext advisoriesDb,
        HaliDataSources dataSources)
    {
        _authDb = authDb;
        _advisoriesDb = advisoriesDb;
        _dataSources = dataSources;
    }

    public async Task<IReadOnlyList<Account>> ListUsersAsync(Guid institutionId, CancellationToken ct)
    {
        return await _authDb.Accounts
            .Where(a => a.InstitutionId == institutionId
                     && a.AccountType == AccountType.InstitutionUser)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<Account?> FindUserInInstitutionAsync(Guid institutionId, Guid userId, CancellationToken ct)
    {
        // Scope filter is server-side — a userId that belongs to a
        // different institution returns null, which the service surfaces
        // as 404 to prevent cross-institution existence probes.
        return _authDb.Accounts.FirstOrDefaultAsync(
            a => a.Id == userId
              && a.InstitutionId == institutionId
              && a.AccountType == AccountType.InstitutionUser,
            ct);
    }

    public Task<int> CountAdminsAsync(Guid institutionId, CancellationToken ct)
    {
        return _authDb.Accounts.CountAsync(
            a => a.InstitutionId == institutionId
              && a.AccountType == AccountType.InstitutionUser
              && a.IsInstitutionAdmin
              && !a.IsBlocked,
            ct);
    }

    public async Task<bool> SetAccountAdminFlagAsync(
        Guid institutionId, Guid userId, bool isInstitutionAdmin, DateTime updatedAt, CancellationToken ct)
    {
        // WHERE-guarded UPDATE so an attacker passing a userId outside
        // the caller's institution scope gets 0 rows updated (returns
        // false → 404) without a racy "read then write".
        int rows = await _authDb.Accounts
            .Where(a => a.Id == userId
                     && a.InstitutionId == institutionId
                     && a.AccountType == AccountType.InstitutionUser)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IsInstitutionAdmin, isInstitutionAdmin)
                .SetProperty(a => a.UpdatedAt, updatedAt),
                ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<InstitutionJurisdiction>> ListJurisdictionsAsync(
        Guid institutionId, CancellationToken ct)
    {
        return await _advisoriesDb.InstitutionJurisdictions
            .Where(j => j.InstitutionId == institutionId)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);
    }

    public Task<Institution?> FindInstitutionAsync(Guid institutionId, CancellationToken ct)
    {
        return _advisoriesDb.Institutions.FirstOrDefaultAsync(i => i.Id == institutionId, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetLocalityDisplayNamesAsync(
        IEnumerable<Guid> localityIds, CancellationToken ct)
    {
        var ids = localityIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Localities live in the signals module; query via the shared
        // data-source pool rather than dragging SignalsDbContext into
        // this repository's DI surface.
        await using var conn = await _dataSources.Signals.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, ward_name FROM localities WHERE id = ANY(@ids)", conn);
        cmd.Parameters.AddWithValue("ids", ids);

        var map = new Dictionary<Guid, string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            map[reader.GetGuid(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        }
        return map;
    }
}

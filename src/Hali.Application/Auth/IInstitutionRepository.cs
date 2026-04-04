using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Auth;

namespace Hali.Application.Auth;

public interface IInstitutionRepository
{
    Task<Institution> CreateInstitutionAsync(Institution institution, CancellationToken ct = default);

    Task SaveInviteAsync(InstitutionInvite invite, CancellationToken ct = default);

    Task<InstitutionInvite?> FindInviteByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task AcceptInviteAsync(InstitutionInvite invite, DateTime acceptedAt, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetAccountIdsByInstitutionAsync(Guid institutionId, CancellationToken ct = default);

    Task BlockAccountsAsync(IEnumerable<Guid> accountIds, CancellationToken ct = default);

    Task RevokeRefreshTokensByAccountIdsAsync(IEnumerable<Guid> accountIds, DateTime revokedAt, CancellationToken ct = default);
}

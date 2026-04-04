using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Auth;

namespace Hali.Application.Auth;

public interface IInstitutionService
{
    Task<CreateInstitutionResponseDto> CreateInstitutionWithInviteAsync(
        Guid adminAccountId,
        CreateInstitutionRequestDto request,
        CancellationToken ct = default);

    Task SetupInstitutionAccountAsync(InstitutionSetupRequestDto request, CancellationToken ct = default);

    Task RevokeInstitutionAccessAsync(Guid institutionId, CancellationToken ct = default);
}

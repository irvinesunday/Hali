using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.InstitutionAdmin;

namespace Hali.Application.InstitutionAdmin;

/// <summary>
/// Phase 2 institution-admin routes (#196). Every operation is scoped
/// to the acting admin's institution — cross-institution targets
/// surface as 404 NotFoundException (institution_admin.user_not_found).
/// </summary>
public interface IInstitutionAdminService
{
    Task<InstitutionAdminUserListResponseDto> ListUsersAsync(Guid institutionId, CancellationToken ct);

    Task<InstitutionAdminUserDetailResponseDto> GetUserAsync(
        Guid institutionId, Guid userId, CancellationToken ct);

    /// <summary>
    /// Issues an invite for a new user in the acting admin's
    /// institution. Elevation of the invited account to institution_admin
    /// is blocked — the role argument must be <c>institution_user</c>.
    /// </summary>
    Task<InviteInstitutionUserResponseDto> InviteUserAsync(
        Guid institutionId, Guid invitedByAccountId,
        InviteInstitutionUserRequestDto request, CancellationToken ct);

    /// <summary>
    /// Changes a user's role within the acting admin's institution.
    /// Rules enforced here:
    ///   * institution_admin → institution_user: allowed, unless the
    ///     caller is demoting themselves while they are the last admin.
    ///   * anything → institution_admin: rejected with
    ///     institution_admin.elevation_requires_approval (deferred).
    ///   * cross-institution target: rejected with
    ///     institution_admin.user_not_found (404) to prevent probing.
    /// </summary>
    Task ChangeUserRoleAsync(
        Guid institutionId, Guid actingAdminId, Guid userId,
        ChangeUserRoleRequestDto request, CancellationToken ct);

    Task<InstitutionAdminScopeResponseDto> GetScopeAsync(
        Guid institutionId, CancellationToken ct);
}

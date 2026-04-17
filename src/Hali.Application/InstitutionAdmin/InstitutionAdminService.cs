using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Contracts.InstitutionAdmin;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Options;

namespace Hali.Application.InstitutionAdmin;

public sealed class InstitutionAdminService : IInstitutionAdminService
{
    private const int InviteExpiryHours = 72;

    private readonly IInstitutionAdminRepository _repo;
    private readonly IAuthRepository _authRepo;
    private readonly IInstitutionRepository _institutionRepo;
    private readonly AuthOptions _authOpts;

    public InstitutionAdminService(
        IInstitutionAdminRepository repo,
        IAuthRepository authRepo,
        IInstitutionRepository institutionRepo,
        IOptions<AuthOptions> authOptions)
    {
        _repo = repo;
        _authRepo = authRepo;
        _institutionRepo = institutionRepo;
        _authOpts = authOptions.Value;
    }

    public async Task<InstitutionAdminUserListResponseDto> ListUsersAsync(
        Guid institutionId, CancellationToken ct)
    {
        var users = await _repo.ListUsersAsync(institutionId, ct);
        var items = users
            .Select(u => new InstitutionAdminUserListItemDto(
                Id: u.Id,
                Email: u.Email,
                DisplayName: u.DisplayName,
                Role: ResolveRole(u),
                CreatedAt: u.CreatedAt))
            .ToList();
        return new InstitutionAdminUserListResponseDto(items);
    }

    public async Task<InstitutionAdminUserDetailResponseDto> GetUserAsync(
        Guid institutionId, Guid userId, CancellationToken ct)
    {
        Account? user = await _repo.FindUserInInstitutionAsync(institutionId, userId, ct);
        if (user is null)
        {
            throw new NotFoundException(
                ErrorCodes.InstitutionAdminUserNotFound, "User not found.");
        }
        return new InstitutionAdminUserDetailResponseDto(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            Role: ResolveRole(user),
            Status: user.Status,
            IsBlocked: user.IsBlocked,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt);
    }

    public async Task<InviteInstitutionUserResponseDto> InviteUserAsync(
        Guid institutionId, Guid invitedByAccountId,
        InviteInstitutionUserRequestDto request, CancellationToken ct)
    {
        // Elevation at invite time is treated the same as runtime elevation —
        // blocked until the approval flow lands. The invited account is
        // created with IsInstitutionAdmin = false; an admin separately
        // promotes them via PUT /role once the approval flow exists.
        if (string.Equals(request.Role, "institution_admin", StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                code: ErrorCodes.InstitutionAdminElevationRequiresApproval,
                message: "Inviting a user directly as institution_admin requires the approval flow.");
        }

        string email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            throw new ValidationException(
                "email is required.",
                code: ErrorCodes.ValidationMissingField,
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["email"] = new[] { "email is required." },
                });
        }

        // Reject emails already bound to an account — invite flow is for
        // brand-new users, not re-assignment of an existing account.
        Account? existing = await _authRepo.FindAccountByEmailAsync(email, ct);
        if (existing is not null)
        {
            throw new ConflictException(
                code: ErrorCodes.InstitutionAdminEmailAlreadyInUse,
                message: "An account with that email already exists.");
        }

        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now.AddHours(InviteExpiryHours);
        string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string tokenHash = AuthService.HashToken(rawToken);

        var invite = new InstitutionInvite
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            InviteTokenHash = tokenHash,
            InvitedByAccountId = invitedByAccountId,
            ExpiresAt = expiresAt,
            CreatedAt = now,
        };
        await _institutionRepo.SaveInviteAsync(invite, ct);

        // Plaintext raw token is intentionally not echoed on the wire —
        // the invite email / magic-link flow surfaces it via a separate
        // controlled channel. The response body only confirms issuance.
        _ = rawToken;
        return new InviteInstitutionUserResponseDto(invite.Id, expiresAt);
    }

    public async Task ChangeUserRoleAsync(
        Guid institutionId, Guid actingAdminId, Guid userId,
        ChangeUserRoleRequestDto request, CancellationToken ct)
    {
        // Elevation guard — deferred by design (#196 scope lock).
        if (string.Equals(request.Role, "institution_admin", StringComparison.Ordinal))
        {
            throw new ForbiddenException(
                code: ErrorCodes.InstitutionAdminElevationRequiresApproval,
                message: "Elevation to institution_admin requires the approval flow.");
        }

        Account? target = await _repo.FindUserInInstitutionAsync(institutionId, userId, ct);
        if (target is null)
        {
            throw new NotFoundException(
                ErrorCodes.InstitutionAdminUserNotFound, "User not found.");
        }

        // Demotion to institution_user is a no-op when the account is
        // already a non-admin — 200 OK with no state change.
        if (!target.IsInstitutionAdmin)
        {
            return;
        }

        // Last-admin-demote guard prevents a lockout where no one can
        // manage the institution. Applies to both self-demotion and
        // demoting another user — both can empty the admin set.
        int currentAdminCount = await _repo.CountAdminsAsync(institutionId, ct);
        if (currentAdminCount <= 1)
        {
            throw new ConflictException(
                code: ErrorCodes.InstitutionAdminLastAdminCannotDemote,
                message: "Cannot demote the last institution_admin of an institution.");
        }

        bool ok = await _repo.SetAccountAdminFlagAsync(
            institutionId, userId, isInstitutionAdmin: false, updatedAt: DateTime.UtcNow, ct);
        if (!ok)
        {
            // Race: the user disappeared from this institution between
            // the read and the write. Surface as the standard 404.
            throw new NotFoundException(
                ErrorCodes.InstitutionAdminUserNotFound, "User not found.");
        }
        _ = actingAdminId; // Reserved for audit-log wiring (follow-up).
    }

    public async Task<InstitutionAdminScopeResponseDto> GetScopeAsync(
        Guid institutionId, CancellationToken ct)
    {
        Institution? institution = await _repo.FindInstitutionAsync(institutionId, ct);
        if (institution is null)
        {
            throw new NotFoundException(
                ErrorCodes.InstitutionAdminUserNotFound, "Institution not found.");
        }

        var jurisdictions = await _repo.ListJurisdictionsAsync(institutionId, ct);
        var localityIds = jurisdictions
            .Where(j => j.LocalityId.HasValue)
            .Select(j => j.LocalityId!.Value)
            .ToArray();
        var displayNames = await _repo.GetLocalityDisplayNamesAsync(localityIds, ct);

        var items = jurisdictions
            .Select(j => new InstitutionAdminScopeJurisdictionDto(
                Id: j.Id,
                LocalityId: j.LocalityId,
                CorridorName: j.CorridorName,
                DisplayName: j.LocalityId.HasValue && displayNames.TryGetValue(j.LocalityId.Value, out var name)
                    ? name
                    : j.CorridorName))
            .ToList();

        return new InstitutionAdminScopeResponseDto(
            InstitutionId: institution.Id,
            InstitutionName: institution.Name,
            Jurisdictions: items);
    }

    // ------------------------------------------------------------------

    private static string ResolveRole(Account account)
        => account.IsInstitutionAdmin ? "institution_admin" : "institution_user";
}

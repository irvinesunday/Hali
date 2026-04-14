using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Contracts.Auth;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hali.Application.Auth;

public class InstitutionService : IInstitutionService
{
    private const int InviteExpiryHours = 72;

    private readonly IInstitutionRepository _repo;
    private readonly IAuthRepository _authRepo;
    private readonly IOtpService _otpService;
    private readonly AuthOptions _opts;

    public InstitutionService(
        IInstitutionRepository repo,
        IAuthRepository authRepo,
        IOtpService otpService,
        IOptions<AuthOptions> opts)
    {
        _repo = repo;
        _authRepo = authRepo;
        _otpService = otpService;
        _opts = opts.Value;
    }

    public async Task<CreateInstitutionResponseDto> CreateInstitutionWithInviteAsync(
        Guid adminAccountId,
        CreateInstitutionRequestDto request,
        CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;

        Institution institution = await _repo.CreateInstitutionAsync(new Institution
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = "government",
            IsVerified = true,
            CreatedAt = now
        }, ct);

        string rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string tokenHash = AuthService.HashToken(rawToken);
        DateTime expiresAt = now.AddHours(InviteExpiryHours);

        await _repo.SaveInviteAsync(new InstitutionInvite
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution.Id,
            InviteTokenHash = tokenHash,
            InvitedByAccountId = adminAccountId,
            ExpiresAt = expiresAt,
            CreatedAt = now
        }, ct);

        string inviteLink = $"{_opts.AppBaseUrl}/institution/setup?token={Uri.EscapeDataString(rawToken)}";

        return new CreateInstitutionResponseDto(institution.Id, inviteLink, expiresAt);
    }

    public async Task SetupInstitutionAccountAsync(InstitutionSetupRequestDto request, CancellationToken ct = default)
    {
        string tokenHash = AuthService.HashToken(request.InviteToken);
        InstitutionInvite? invite = await _repo.FindInviteByTokenHashAsync(tokenHash, ct);

        DateTime now = DateTime.UtcNow;

        if (invite == null)
            throw new ValidationException("Invalid invite token.", code: "invite.invalid");

        if (invite.ExpiresAt <= now)
            throw new ValidationException("Invite token has expired.", code: "invite.expired");

        if (invite.AcceptedAt != null)
            throw new ConflictException("invite.already_accepted", "Invite has already been accepted.");

        // Create account with institution role
        Account? existing = await _authRepo.FindAccountByPhoneAsync(request.PhoneNumber, ct);
        if (existing == null)
        {
            await _authRepo.CreateAccountAsync(new Account
            {
                Id = Guid.NewGuid(),
                PhoneE164 = request.PhoneNumber,
                IsPhoneVerified = false,
                AccountType = AccountType.InstitutionUser,
                InstitutionId = invite.InstitutionId,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }
        else
        {
            // Phone already registered — update to institution role for this institution
            existing.AccountType = AccountType.InstitutionUser;
            existing.InstitutionId = invite.InstitutionId;
            existing.UpdatedAt = now;
            await _authRepo.UpdateAccountAsync(existing, ct);
        }

        await _otpService.RequestOtpAsync(request.PhoneNumber, AuthMethod.PhoneOtp, ct);
    }

    public async Task RevokeInstitutionAccessAsync(Guid institutionId, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;

        IReadOnlyList<Guid> accountIds = await _repo.GetAccountIdsByInstitutionAsync(institutionId, ct);
        if (accountIds.Count == 0)
            return;

        await _repo.BlockAccountsAsync(accountIds, ct);
        await _repo.RevokeRefreshTokensByAccountIdsAsync(accountIds, now, ct);
    }
}

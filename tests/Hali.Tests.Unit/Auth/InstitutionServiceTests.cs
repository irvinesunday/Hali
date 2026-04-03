using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Contracts.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace Hali.Tests.Unit.Auth;

public class InstitutionServiceTests
{
    private readonly IInstitutionRepository _repo = Substitute.For<IInstitutionRepository>();
    private readonly IAuthRepository _authRepo = Substitute.For<IAuthRepository>();
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();

    private InstitutionService CreateService(AuthOptions? opts = null)
    {
        var options = Options.Create(opts ?? new AuthOptions
        {
            JwtSecret = "test-secret-key-must-be-at-least-32-chars-long!!",
            JwtIssuer = "hali-test",
            JwtAudience = "hali-test",
            JwtExpiryMinutes = 60,
            RefreshTokenExpiryDays = 30,
            AppBaseUrl = "https://app.gethali.app"
        });
        return new InstitutionService(_repo, _authRepo, _otpService, options);
    }

    [Fact]
    public async Task AdminCreateInstitution_CreatesInviteAndReturnsLink()
    {
        Guid adminId = Guid.NewGuid();
        _repo.CreateInstitutionAsync(Arg.Any<Domain.Entities.Advisories.Institution>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<Domain.Entities.Advisories.Institution>(0));

        var svc = CreateService();
        var result = await svc.CreateInstitutionWithInviteAsync(adminId, new CreateInstitutionRequestDto(
            "Nairobi Water & Sewerage Company", new List<Guid>(), "ops@nwsc.co.ke"));

        Assert.NotEqual(Guid.Empty, result.InstitutionId);
        Assert.Contains("/institution/setup?token=", result.InviteLink);
        Assert.True(result.InviteExpiresAt > DateTime.UtcNow);
        await _repo.Received(1).SaveInviteAsync(Arg.Any<InstitutionInvite>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstitutionSetup_ValidInvite_CreatesAccountAndSendsOtp()
    {
        string rawToken = "test-token";
        string tokenHash = AuthService.HashToken(rawToken);
        Guid institutionId = Guid.NewGuid();

        _repo.FindInviteByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(new InstitutionInvite
            {
                Id = Guid.NewGuid(),
                InstitutionId = institutionId,
                InviteTokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                AcceptedAt = null,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            });
        _authRepo.FindAccountByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);
        _authRepo.CreateAccountAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<Account>(0));

        var svc = CreateService();
        await svc.SetupInstitutionAccountAsync(
            new InstitutionSetupRequestDto(rawToken, "+254700000002"));

        await _authRepo.Received(1).CreateAccountAsync(
            Arg.Is<Account>(a =>
                a.PhoneE164 == "+254700000002" &&
                a.AccountType == AccountType.InstitutionUser &&
                a.InstitutionId == institutionId),
            Arg.Any<CancellationToken>());
        await _otpService.Received(1).RequestOtpAsync("+254700000002", AuthMethod.PhoneOtp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InstitutionSetup_ExpiredInvite_Returns400()
    {
        string rawToken = "expired-token";
        string tokenHash = AuthService.HashToken(rawToken);

        _repo.FindInviteByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(new InstitutionInvite
            {
                Id = Guid.NewGuid(),
                InstitutionId = Guid.NewGuid(),
                InviteTokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                AcceptedAt = null,
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            });

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetupInstitutionAccountAsync(new InstitutionSetupRequestDto(rawToken, "+254700000002")));
        Assert.Equal("INVITE_EXPIRED", ex.Message);
    }

    [Fact]
    public async Task InstitutionSetup_AlreadyAcceptedInvite_Returns400()
    {
        string rawToken = "accepted-token";
        string tokenHash = AuthService.HashToken(rawToken);

        _repo.FindInviteByTokenHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(new InstitutionInvite
            {
                Id = Guid.NewGuid(),
                InstitutionId = Guid.NewGuid(),
                InviteTokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                AcceptedAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetupInstitutionAccountAsync(new InstitutionSetupRequestDto(rawToken, "+254700000002")));
        Assert.Equal("INVITE_ALREADY_ACCEPTED", ex.Message);
    }

    [Fact]
    public async Task InstitutionSetup_InvalidToken_Returns400()
    {
        _repo.FindInviteByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((InstitutionInvite?)null);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetupInstitutionAccountAsync(new InstitutionSetupRequestDto("bogus", "+254700000002")));
        Assert.Equal("INVITE_INVALID", ex.Message);
    }

    [Fact]
    public async Task AdminRevoke_BlocksAccountAndRevokesTokens()
    {
        Guid institutionId = Guid.NewGuid();
        var accountIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _repo.GetAccountIdsByInstitutionAsync(institutionId, Arg.Any<CancellationToken>())
            .Returns(accountIds);

        var svc = CreateService();
        await svc.RevokeInstitutionAccessAsync(institutionId);

        await _repo.Received(1).BlockAccountsAsync(accountIds, Arg.Any<CancellationToken>());
        await _repo.Received(1).RevokeRefreshTokensByAccountIdsAsync(accountIds, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}

public class InstitutionJwtClaimTests
{
    private readonly IAuthRepository _repo = Substitute.For<IAuthRepository>();
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();

    private AuthService CreateService()
    {
        var opts = Options.Create(new AuthOptions
        {
            JwtSecret = "test-secret-key-must-be-at-least-32-chars-long!!",
            JwtIssuer = "hali-test",
            JwtAudience = "hali-test",
            JwtExpiryMinutes = 60,
            RefreshTokenExpiryDays = 30
        });
        return new AuthService(_repo, _otpService, opts);
    }

    [Fact]
    public async Task InstitutionVerify_IssuedJwtContainsInstitutionClaims()
    {
        Guid accountId = Guid.NewGuid();
        Guid institutionId = Guid.NewGuid();
        Guid deviceId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        _otpService.ConsumeOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _repo.FindAccountByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new Account
        {
            Id = accountId,
            PhoneE164 = "+254700000002",
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            CreatedAt = now,
            UpdatedAt = now
        });
        _repo.FindAccountByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(new Account
        {
            Id = accountId,
            PhoneE164 = "+254700000002",
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            CreatedAt = now,
            UpdatedAt = now
        });
        _repo.UpsertDeviceAsync(Arg.Any<string>(), accountId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Device { Id = deviceId, AccountId = accountId, DeviceFingerprintHash = "fp", CreatedAt = now, LastSeenAt = now });

        var svc = CreateService();
        var result = await svc.AuthenticateAsync(new VerifyOtpRequestDto("+254700000002", "123456", "fp", "ios", "1.0.0", null));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        Assert.Equal(institutionId.ToString(), jwt.Claims.First(c => c.Type == "institution_id").Value);
    }

    [Fact]
    public async Task InstitutionVerify_IssuedJwtContainsRole_Institution()
    {
        Guid accountId = Guid.NewGuid();
        Guid institutionId = Guid.NewGuid();
        Guid deviceId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        _otpService.ConsumeOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _repo.FindAccountByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new Account
        {
            Id = accountId,
            PhoneE164 = "+254700000002",
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            CreatedAt = now,
            UpdatedAt = now
        });
        _repo.FindAccountByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns(new Account
        {
            Id = accountId,
            PhoneE164 = "+254700000002",
            AccountType = AccountType.InstitutionUser,
            InstitutionId = institutionId,
            CreatedAt = now,
            UpdatedAt = now
        });
        _repo.UpsertDeviceAsync(Arg.Any<string>(), accountId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Device { Id = deviceId, AccountId = accountId, DeviceFingerprintHash = "fp", CreatedAt = now, LastSeenAt = now });

        var svc = CreateService();
        var result = await svc.AuthenticateAsync(new VerifyOtpRequestDto("+254700000002", "123456", "fp", "ios", "1.0.0", null));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.AccessToken);

        // JwtSecurityTokenHandler maps "role" to the long ClaimTypes.Role URI
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)
            ?? jwt.Claims.FirstOrDefault(c => c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("institution", roleClaim!.Value);
    }
}

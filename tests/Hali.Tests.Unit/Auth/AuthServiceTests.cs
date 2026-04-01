using Hali.Application.Auth;
using Hali.Contracts.Auth;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hali.Tests.Unit.Auth;

public class AuthServiceTests
{
    private readonly IAuthRepository _repo = Substitute.For<IAuthRepository>();
    private readonly IOtpService _otpService = Substitute.For<IOtpService>();

    private AuthService CreateService(AuthOptions? opts = null)
    {
        var options = Options.Create(opts ?? new AuthOptions
        {
            JwtSecret = "test-secret-key-must-be-at-least-32-chars-long!!",
            JwtIssuer = "hali-test",
            JwtAudience = "hali-test",
            JwtExpiryMinutes = 60,
            RefreshTokenExpiryDays = 30
        });
        return new AuthService(_repo, _otpService, options);
    }

    private static VerifyOtpRequestDto MakeVerifyRequest(string destination = "+254712345678", string otp = "123456")
        => new(destination, otp, "device-fp-hash-abc", "ios", "1.0.0", null);

    [Fact]
    public async Task Authenticate_WithInvalidOtp_Throws()
    {
        _otpService.ConsumeOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AuthenticateAsync(MakeVerifyRequest()));

        Assert.Equal("OTP_INVALID", ex.Message);
    }

    [Fact]
    public async Task Authenticate_WithValidOtp_ReturnsTokenPair()
    {
        var accountId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _otpService.ConsumeOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _repo.FindAccountByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Account { Id = accountId, PhoneE164 = "+254712345678", CreatedAt = now, UpdatedAt = now });

        _repo.UpsertDeviceAsync(Arg.Any<string>(), accountId, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Device { Id = deviceId, AccountId = accountId, DeviceFingerprintHash = "device-fp-hash-abc", CreatedAt = now, LastSeenAt = now });

        var svc = CreateService();
        var result = await svc.AuthenticateAsync(MakeVerifyRequest());

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);

        await _repo.Received(1).SaveRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.AccountId == accountId && t.DeviceId == deviceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Authenticate_WhenAccountDoesNotExist_CreatesAccount()
    {
        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _otpService.ConsumeOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _repo.FindAccountByPhoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Account?)null);

        _repo.CreateAccountAsync(Arg.Any<Account>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<Account>(0));

        _repo.UpsertDeviceAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Device { Id = deviceId, DeviceFingerprintHash = "device-fp-hash-abc", CreatedAt = now, LastSeenAt = now });

        var svc = CreateService();
        await svc.AuthenticateAsync(MakeVerifyRequest());

        await _repo.Received(1).CreateAccountAsync(
            Arg.Is<Account>(a => a.PhoneE164 == "+254712345678" && a.IsPhoneVerified),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_WithValidToken_RevokesOldAndIssuesNew()
    {
        var accountId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var plainToken = "some-valid-refresh-token";
        var tokenHash = AuthService.HashToken(plainToken);

        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            AccountId = accountId,
            DeviceId = deviceId,
            ExpiresAt = now.AddDays(30)
        };

        _repo.FindActiveRefreshTokenAsync(tokenHash, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(stored);

        var svc = CreateService();
        var result = await svc.RefreshAsync(plainToken);

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotEqual(plainToken, result.RefreshToken);

        await _repo.Received(1).RevokeRefreshTokenAsync(stored, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveRefreshTokenAsync(
            Arg.Is<RefreshToken>(t => t.AccountId == accountId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_Throws()
    {
        _repo.FindActiveRefreshTokenAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var svc = CreateService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RefreshAsync("bad-token"));

        Assert.Equal("REFRESH_TOKEN_INVALID", ex.Message);
    }

    [Fact]
    public async Task Logout_WithValidToken_RevokesToken()
    {
        var stored = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = AuthService.HashToken("valid-token"),
            AccountId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _repo.FindActiveRefreshTokenAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(stored);

        var svc = CreateService();
        await svc.RevokeAsync("valid-token");

        await _repo.Received(1).RevokeRefreshTokenAsync(stored, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithUnknownToken_DoesNotThrow()
    {
        _repo.FindActiveRefreshTokenAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        var svc = CreateService();
        await svc.RevokeAsync("nonexistent-token");

        await _repo.DidNotReceive().RevokeRefreshTokenAsync(Arg.Any<RefreshToken>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void HashToken_IsDeterministic()
    {
        var h1 = AuthService.HashToken("my-token");
        var h2 = AuthService.HashToken("my-token");
        var h3 = AuthService.HashToken("other-token");

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
    }
}

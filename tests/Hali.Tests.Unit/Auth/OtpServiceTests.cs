using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Hali.Tests.Unit.Auth;

public class OtpServiceTests
{
    private readonly IAuthRepository _repo = Substitute.For<IAuthRepository>();
    private readonly ISmsProvider _sms = Substitute.For<ISmsProvider>();
    private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>();

    private OtpService CreateService(OtpOptions? opts = null)
    {
        var options = Options.Create(opts ?? new OtpOptions { Length = 6, TtlMinutes = 10, MaxRequestsPerWindow = 5, WindowMinutes = 10 });
        return new OtpService(_repo, _sms, _rateLimiter, options);
    }

    [Fact]
    public async Task RequestOtp_WhenRateLimited_ThrowsWithCorrectCode()
    {
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var svc = CreateService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RequestOtpAsync("+254712345678", AuthMethod.PhoneOtp));

        Assert.Equal("OTP_RATE_LIMITED", ex.Message);
        await _sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestOtp_WhenAllowed_SavesChallengeAndSendsSms()
    {
        _rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var svc = CreateService();
        await svc.RequestOtpAsync("+254712345678", AuthMethod.PhoneOtp);

        await _repo.Received(1).SaveOtpChallengeAsync(
            Arg.Is<OtpChallenge>(c => c.Destination == "+254712345678" && c.AuthMethod == AuthMethod.PhoneOtp),
            Arg.Any<CancellationToken>());

        await _sms.Received(1).SendAsync(
            "+254712345678",
            Arg.Is<string>(m => m.Contains("verification code")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumeOtp_WhenNoActiveChallenge_ReturnsFalse()
    {
        _repo.FindActiveOtpAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((OtpChallenge?)null);

        var svc = CreateService();
        var result = await svc.ConsumeOtpAsync("+254712345678", "123456");

        Assert.False(result);
    }

    [Fact]
    public async Task ConsumeOtp_WithWrongOtp_ReturnsFalse()
    {
        var destination = "+254712345678";
        var challenge = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Destination = destination,
            OtpHash = OtpService.HashOtp("999999", destination),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            AuthMethod = AuthMethod.PhoneOtp,
            CreatedAt = DateTime.UtcNow
        };

        _repo.FindActiveOtpAsync(destination, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(challenge);

        var svc = CreateService();
        var result = await svc.ConsumeOtpAsync(destination, "123456");

        Assert.False(result);
        await _repo.DidNotReceive().ConsumeOtpAsync(Arg.Any<OtpChallenge>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumeOtp_WithCorrectOtp_ReturnsTrueAndConsumes()
    {
        var destination = "+254712345678";
        var otp = "123456";
        var challenge = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Destination = destination,
            OtpHash = OtpService.HashOtp(otp, destination),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            AuthMethod = AuthMethod.PhoneOtp,
            CreatedAt = DateTime.UtcNow
        };

        _repo.FindActiveOtpAsync(destination, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(challenge);

        var svc = CreateService();
        var result = await svc.ConsumeOtpAsync(destination, otp);

        Assert.True(result);
        await _repo.Received(1).ConsumeOtpAsync(challenge, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void HashOtp_IsDeterministicAndCaseSensitiveOnInput()
    {
        var h1 = OtpService.HashOtp("123456", "+254712345678");
        var h2 = OtpService.HashOtp("123456", "+254712345678");
        var h3 = OtpService.HashOtp("654321", "+254712345678");

        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
    }

    [Fact]
    public void HashOtp_IsCaseInsensitiveOnDestination()
    {
        var h1 = OtpService.HashOtp("123456", "USER@EXAMPLE.COM");
        var h2 = OtpService.HashOtp("123456", "user@example.com");

        Assert.Equal(h1, h2);
    }
}

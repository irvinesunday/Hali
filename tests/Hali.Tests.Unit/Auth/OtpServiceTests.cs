using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Hali.Tests.Unit.Auth;

public class OtpServiceTests
{
	private readonly IAuthRepository _repo = Substitute.For<IAuthRepository>(Array.Empty<object>());

	private readonly ISmsProvider _sms = Substitute.For<ISmsProvider>(Array.Empty<object>());

	private readonly IRateLimiter _rateLimiter = Substitute.For<IRateLimiter>(Array.Empty<object>());

	private OtpService CreateService(OtpOptions? opts = null)
	{
		IOptions<OtpOptions> opts2 = Options.Create(opts ?? new OtpOptions
		{
			Length = 6,
			TtlMinutes = 10,
			MaxRequestsPerWindow = 5,
			WindowMinutes = 10
		});
		return new OtpService(_repo, _sms, _rateLimiter, opts2);
	}

	[Fact]
	public async Task RequestOtp_WhenRateLimited_ThrowsWithCorrectCode()
	{
		_rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
		OtpService svc = CreateService();
		var ex = await Assert.ThrowsAsync<RateLimitException>(() => svc.RequestOtpAsync("+254712345678", AuthMethod.PhoneOtp));
		Assert.Equal("auth.otp_rate_limited", ex.Code);
		await _sms.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task RequestOtp_WhenAllowed_SavesChallengeAndSendsSms()
	{
		_rateLimiter.IsAllowedAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
		OtpService svc = CreateService();
		await svc.RequestOtpAsync("+254712345678", AuthMethod.PhoneOtp);
		await _repo.Received(1).SaveOtpChallengeAsync(Arg.Is((OtpChallenge c) => c.Destination == "+254712345678" && (int)c.AuthMethod == 0), Arg.Any<CancellationToken>());
		await _sms.Received(1).SendAsync("+254712345678", Arg.Is((string m) => m.Contains("verification code")), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConsumeOtp_WhenNoActiveChallenge_ReturnsFalse()
	{
		_repo.FindActiveOtpAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(null, Array.Empty<OtpChallenge>());
		OtpService svc = CreateService();
		Assert.False(await svc.ConsumeOtpAsync("+254712345678", "123456"));
	}

	[Fact]
	public async Task ConsumeOtp_WithWrongOtp_ReturnsFalse()
	{
		string destination = "+254712345678";
		SubstituteExtensions.Returns(returnThis: new OtpChallenge
		{
			Id = Guid.NewGuid(),
			Destination = destination,
			OtpHash = OtpService.HashOtp("999999", destination),
			ExpiresAt = DateTime.UtcNow.AddMinutes(5.0),
			AuthMethod = AuthMethod.PhoneOtp,
			CreatedAt = DateTime.UtcNow
		}, value: _repo.FindActiveOtpAsync(destination, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()), returnThese: Array.Empty<OtpChallenge>());
		OtpService svc = CreateService();
		Assert.False(await svc.ConsumeOtpAsync(destination, "123456"));
		await _repo.DidNotReceive().ConsumeOtpAsync(Arg.Any<OtpChallenge>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ConsumeOtp_WithCorrectOtp_ReturnsTrueAndConsumes()
	{
		string destination = "+254712345678";
		string otp = "123456";
		OtpChallenge challenge = new OtpChallenge
		{
			Id = Guid.NewGuid(),
			Destination = destination,
			OtpHash = OtpService.HashOtp(otp, destination),
			ExpiresAt = DateTime.UtcNow.AddMinutes(5.0),
			AuthMethod = AuthMethod.PhoneOtp,
			CreatedAt = DateTime.UtcNow
		};
		_repo.FindActiveOtpAsync(destination, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(challenge);
		OtpService svc = CreateService();
		Assert.True(await svc.ConsumeOtpAsync(destination, otp));
		await _repo.Received(1).ConsumeOtpAsync(challenge, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public void HashOtp_IsDeterministicAndCaseSensitiveOnInput()
	{
		string expected = OtpService.HashOtp("123456", "+254712345678");
		string actual = OtpService.HashOtp("123456", "+254712345678");
		string actual2 = OtpService.HashOtp("654321", "+254712345678");
		Assert.Equal(expected, actual);
		Assert.NotEqual(expected, actual2);
	}

	[Fact]
	public void HashOtp_IsCaseInsensitiveOnDestination()
	{
		string expected = OtpService.HashOtp("123456", "USER@EXAMPLE.COM");
		string actual = OtpService.HashOtp("123456", "user@example.com");
		Assert.Equal(expected, actual);
	}
}

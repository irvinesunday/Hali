using System.Security.Cryptography;
using System.Text;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hali.Application.Auth;

public class OtpService : IOtpService
{
    private readonly IAuthRepository _repo;
    private readonly ISmsProvider _sms;
    private readonly IRateLimiter _rateLimiter;
    private readonly OtpOptions _opts;

    public OtpService(
        IAuthRepository repo,
        ISmsProvider sms,
        IRateLimiter rateLimiter,
        IOptions<OtpOptions> opts)
    {
        _repo = repo;
        _sms = sms;
        _rateLimiter = rateLimiter;
        _opts = opts.Value;
    }

    public async Task RequestOtpAsync(string destination, AuthMethod authMethod, CancellationToken ct = default)
    {
        var key = $"rl:otp:{destination}";
        var allowed = await _rateLimiter.IsAllowedAsync(key, _opts.MaxRequestsPerWindow, TimeSpan.FromMinutes(_opts.WindowMinutes), ct);
        if (!allowed)
            throw new InvalidOperationException("OTP_RATE_LIMITED");

        var otp = GenerateOtp(_opts.Length);
        var now = DateTime.UtcNow;

        var challenge = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            AuthMethod = authMethod,
            Destination = destination,
            OtpHash = HashOtp(otp, destination),
            ExpiresAt = now.AddMinutes(_opts.TtlMinutes),
            CreatedAt = now
        };

        await _repo.SaveOtpChallengeAsync(challenge, ct);
        await _sms.SendAsync(destination, $"Your Hali verification code is {otp}. Valid for {_opts.TtlMinutes} minutes.", ct);
    }

    public async Task<bool> ConsumeOtpAsync(string destination, string otp, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var challenge = await _repo.FindActiveOtpAsync(destination, now, ct);
        if (challenge is null)
            return false;

        var expectedHash = HashOtp(otp, destination);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(challenge.OtpHash),
                Encoding.UTF8.GetBytes(expectedHash)))
            return false;

        await _repo.ConsumeOtpAsync(challenge, now, ct);
        return true;
    }

    private static string GenerateOtp(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString().PadLeft(length, '0');
    }

    public static string HashOtp(string otp, string destination)
    {
        var input = $"{otp}:{destination.ToLowerInvariant()}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

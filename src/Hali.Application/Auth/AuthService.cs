using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Hali.Contracts.Auth;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hali.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _repo;
    private readonly IOtpService _otpService;
    private readonly AuthOptions _opts;

    public AuthService(IAuthRepository repo, IOtpService otpService, IOptions<AuthOptions> opts)
    {
        _repo = repo;
        _otpService = otpService;
        _opts = opts.Value;
    }

    public async Task<TokenResponseDto> AuthenticateAsync(VerifyOtpRequestDto request, CancellationToken ct = default)
    {
        var valid = await _otpService.ConsumeOtpAsync(request.Destination, request.Otp, ct);
        if (!valid)
            throw new InvalidOperationException("OTP_INVALID");

        var now = DateTime.UtcNow;

        var account = await _repo.FindAccountByPhoneAsync(request.Destination, ct)
                      ?? await _repo.CreateAccountAsync(new Account
                      {
                          Id = Guid.NewGuid(),
                          PhoneE164 = request.Destination,
                          IsPhoneVerified = true,
                          CreatedAt = now,
                          UpdatedAt = now
                      }, ct);

        var device = await _repo.UpsertDeviceAsync(
            request.DeviceFingerprintHash,
            account.Id,
            request.Platform,
            request.AppVersion,
            request.ExpoPushToken,
            now,
            ct);

        return await IssueTokenPairAsync(account.Id, device.Id, now, ct);
    }

    public async Task<TokenResponseDto> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var stored = await _repo.FindActiveRefreshTokenAsync(tokenHash, now, ct);
        if (stored is null)
            throw new InvalidOperationException("REFRESH_TOKEN_INVALID");

        await _repo.RevokeRefreshTokenAsync(stored, now, ct);
        return await IssueTokenPairAsync(stored.AccountId, stored.DeviceId ?? Guid.Empty, now, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var stored = await _repo.FindActiveRefreshTokenAsync(tokenHash, now, ct);
        if (stored is null)
            return;

        await _repo.RevokeRefreshTokenAsync(stored, now, ct);
    }

    private async Task<TokenResponseDto> IssueTokenPairAsync(Guid accountId, Guid deviceId, DateTime now, CancellationToken ct)
    {
        var accessToken = IssueAccessToken(accountId, now);
        var (plainRefreshToken, refreshEntity) = CreateRefreshToken(accountId, deviceId, now);

        await _repo.SaveRefreshTokenAsync(refreshEntity, ct);

        return new TokenResponseDto(accessToken, plainRefreshToken, _opts.JwtExpiryMinutes * 60);
    }

    private string IssueAccessToken(Guid accountId, DateTime now)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _opts.JwtIssuer,
            audience: _opts.JwtAudience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_opts.JwtExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private (string PlainToken, RefreshToken Entity) CreateRefreshToken(Guid accountId, Guid deviceId, DateTime now)
    {
        var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = HashToken(plain);

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = hash,
            AccountId = accountId,
            DeviceId = deviceId == Guid.Empty ? null : deviceId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_opts.RefreshTokenExpiryDays)
        };

        return (plain, entity);
    }

    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Contracts.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
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

    public async Task<TokenResponseDto> AuthenticateAsync(VerifyOtpRequestDto request, CancellationToken ct = default(CancellationToken))
    {
        if (!(await _otpService.ConsumeOtpAsync(request.Destination, request.Otp, ct)))
        {
            throw new ValidationException("Invalid or expired OTP.", code: "auth.otp_invalid");
        }
        DateTime now = DateTime.UtcNow;
        Account account = await _repo.FindAccountByPhoneAsync(request.Destination, ct);
        if (account == null)
        {
            account = await _repo.CreateAccountAsync(new Account
            {
                Id = Guid.NewGuid(),
                PhoneE164 = request.Destination,
                IsPhoneVerified = true,
                CreatedAt = now,
                UpdatedAt = now
            }, ct);
        }
        Account account2 = account;
        return await IssueTokenPairAsync(deviceId: (await _repo.UpsertDeviceAsync(request.DeviceFingerprintHash, account2.Id, request.Platform, request.AppVersion, request.ExpoPushToken, now, ct)).Id, accountId: account2.Id, now: now, ct: ct);
    }

    public async Task<TokenResponseDto> RefreshAsync(string refreshToken, CancellationToken ct = default(CancellationToken))
    {
        DateTime now = DateTime.UtcNow;
        string tokenHash = HashToken(refreshToken);
        RefreshToken stored = await _repo.FindActiveRefreshTokenAsync(tokenHash, now, ct);
        if (stored == null)
        {
            throw new UnauthorizedException(
                code: "auth.refresh_token_invalid",
                message: "Invalid or expired refresh token.");
        }
        await _repo.RevokeRefreshTokenAsync(stored, now, ct);
        return await IssueTokenPairAsync(stored.AccountId, stored.DeviceId ?? Guid.Empty, now, ct);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default(CancellationToken))
    {
        DateTime now = DateTime.UtcNow;
        string tokenHash = HashToken(refreshToken);
        RefreshToken stored = await _repo.FindActiveRefreshTokenAsync(tokenHash, now, ct);
        if (stored != null)
        {
            await _repo.RevokeRefreshTokenAsync(stored, now, ct);
        }
    }

    private async Task<TokenResponseDto> IssueTokenPairAsync(Guid accountId, Guid deviceId, DateTime now, CancellationToken ct)
    {
        Account? account = await _repo.FindAccountByIdAsync(accountId, ct);
        string accessToken = IssueAccessToken(accountId, account?.AccountType ?? AccountType.Citizen, account?.InstitutionId, now);
        var (plainRefreshToken, refreshEntity) = CreateRefreshToken(accountId, deviceId, now);
        await _repo.SaveRefreshTokenAsync(refreshEntity, ct);
        return new TokenResponseDto(accessToken, plainRefreshToken, _opts.JwtExpiryMinutes * 60);
    }

    private string IssueAccessToken(Guid accountId, AccountType accountType, Guid? institutionId, DateTime now)
    {
        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.JwtSecret));
        SigningCredentials signingCredentials = new SigningCredentials(key, "HS256");
        string role = accountType switch
        {
            AccountType.InstitutionUser => "institution",
            AccountType.Admin => "admin",
            _ => "citizen"
        };
        var claims = new List<Claim>
        {
            new Claim("sub", accountId.ToString()),
            new Claim("jti", Guid.NewGuid().ToString()),
            new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), "http://www.w3.org/2001/XMLSchema#integer64"),
            new Claim(ClaimTypes.Role, role)
        };
        if (institutionId.HasValue)
            claims.Add(new Claim("institution_id", institutionId.Value.ToString()));
        JwtSecurityToken token = new JwtSecurityToken(_opts.JwtIssuer, _opts.JwtAudience, claims, now, now.AddMinutes(_opts.JwtExpiryMinutes), signingCredentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private (string PlainToken, RefreshToken Entity) CreateRefreshToken(Guid accountId, Guid deviceId, DateTime now)
    {
        string text = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string tokenHash = HashToken(text);
        RefreshToken item = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            AccountId = accountId,
            DeviceId = ((deviceId == Guid.Empty) ? ((Guid?)null) : new Guid?(deviceId)),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_opts.RefreshTokenExpiryDays)
        };
        return (PlainToken: text, Entity: item);
    }

    public static string HashToken(string token)
    {
        byte[] inArray = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(inArray).ToLowerInvariant();
    }
}

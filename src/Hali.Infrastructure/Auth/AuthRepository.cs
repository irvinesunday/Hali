using Hali.Application.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Infrastructure.Data.Auth;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Auth;

public class AuthRepository : IAuthRepository
{
    private readonly AuthDbContext _db;

    public AuthRepository(AuthDbContext db)
    {
        _db = db;
    }

    public Task<Account?> FindAccountByPhoneAsync(string phoneE164, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.PhoneE164 == phoneE164, ct);

    public async Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default)
    {
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public async Task SaveOtpChallengeAsync(OtpChallenge challenge, CancellationToken ct = default)
    {
        _db.OtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(ct);
    }

    public Task<OtpChallenge?> FindActiveOtpAsync(string destination, DateTime now, CancellationToken ct = default)
        => _db.OtpChallenges
            .Where(o => o.Destination == destination && o.ExpiresAt > now && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task ConsumeOtpAsync(OtpChallenge challenge, DateTime consumedAt, CancellationToken ct = default)
    {
        challenge.ConsumedAt = consumedAt;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Device> UpsertDeviceAsync(
        string fingerprintHash,
        Guid accountId,
        string? platform,
        string? appVersion,
        string? expoPushToken,
        DateTime now,
        CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceFingerprintHash == fingerprintHash, ct);

        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                DeviceFingerprintHash = fingerprintHash,
                Platform = platform,
                AppVersion = appVersion,
                ExpoPushToken = expoPushToken,
                CreatedAt = now,
                LastSeenAt = now
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.AccountId = accountId;
            device.LastSeenAt = now;
            if (platform is not null) device.Platform = platform;
            if (appVersion is not null) device.AppVersion = appVersion;
            if (expoPushToken is not null) device.ExpoPushToken = expoPushToken;
        }

        await _db.SaveChangesAsync(ct);
        return device;
    }

    public Task<RefreshToken?> FindActiveRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken ct = default)
        => _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.ExpiresAt > now && t.RevokedAt == null, ct);

    public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token, DateTime revokedAt, CancellationToken ct = default)
    {
        token.RevokedAt = revokedAt;
        await _db.SaveChangesAsync(ct);
    }
}

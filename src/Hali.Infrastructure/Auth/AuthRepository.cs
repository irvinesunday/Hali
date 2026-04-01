using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

	public Task<Account?> FindAccountByPhoneAsync(string phoneE164, CancellationToken ct = default(CancellationToken))
	{
		return _db.Accounts.FirstOrDefaultAsync((Account a) => a.PhoneE164 == phoneE164, ct);
	}

	public async Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default(CancellationToken))
	{
		_db.Accounts.Add(account);
		await _db.SaveChangesAsync(ct);
		return account;
	}

	public async Task SaveOtpChallengeAsync(OtpChallenge challenge, CancellationToken ct = default(CancellationToken))
	{
		_db.OtpChallenges.Add(challenge);
		await _db.SaveChangesAsync(ct);
	}

	public Task<OtpChallenge?> FindActiveOtpAsync(string destination, DateTime now, CancellationToken ct = default(CancellationToken))
	{
		return (from o in _db.OtpChallenges
			where o.Destination == destination && o.ExpiresAt > now && o.ConsumedAt == null
			orderby o.CreatedAt descending
			select o).FirstOrDefaultAsync(ct);
	}

	public async Task ConsumeOtpAsync(OtpChallenge challenge, DateTime consumedAt, CancellationToken ct = default(CancellationToken))
	{
		challenge.ConsumedAt = consumedAt;
		await _db.SaveChangesAsync(ct);
	}

	public async Task<Device> UpsertDeviceAsync(string fingerprintHash, Guid accountId, string? platform, string? appVersion, string? expoPushToken, DateTime now, CancellationToken ct = default(CancellationToken))
	{
		Device device = await _db.Devices.FirstOrDefaultAsync((Device d) => d.DeviceFingerprintHash == fingerprintHash, ct);
		if (device == null)
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
			if (platform != null)
			{
				device.Platform = platform;
			}
			if (appVersion != null)
			{
				device.AppVersion = appVersion;
			}
			if (expoPushToken != null)
			{
				device.ExpoPushToken = expoPushToken;
			}
		}
		await _db.SaveChangesAsync(ct);
		return device;
	}

	public Task<RefreshToken?> FindActiveRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken ct = default(CancellationToken))
	{
		return _db.RefreshTokens.FirstOrDefaultAsync((RefreshToken t) => t.TokenHash == tokenHash && t.ExpiresAt > now && t.RevokedAt == null, ct);
	}

	public async Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default(CancellationToken))
	{
		_db.RefreshTokens.Add(token);
		await _db.SaveChangesAsync(ct);
	}

	public async Task RevokeRefreshTokenAsync(RefreshToken token, DateTime revokedAt, CancellationToken ct = default(CancellationToken))
	{
		token.RevokedAt = revokedAt;
		await _db.SaveChangesAsync(ct);
	}

	public Task<Device?> FindDeviceByFingerprintAsync(string fingerprintHash, CancellationToken ct = default(CancellationToken))
	{
		return _db.Devices.FirstOrDefaultAsync((Device d) => d.DeviceFingerprintHash == fingerprintHash, ct);
	}
}

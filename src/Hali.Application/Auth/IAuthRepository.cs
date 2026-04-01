using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Auth;

namespace Hali.Application.Auth;

public interface IAuthRepository
{
	Task<Account?> FindAccountByPhoneAsync(string phoneE164, CancellationToken ct = default(CancellationToken));

	Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default(CancellationToken));

	Task SaveOtpChallengeAsync(OtpChallenge challenge, CancellationToken ct = default(CancellationToken));

	Task<OtpChallenge?> FindActiveOtpAsync(string destination, DateTime now, CancellationToken ct = default(CancellationToken));

	Task ConsumeOtpAsync(OtpChallenge challenge, DateTime consumedAt, CancellationToken ct = default(CancellationToken));

	Task<Device> UpsertDeviceAsync(string fingerprintHash, Guid accountId, string? platform, string? appVersion, string? expoPushToken, DateTime now, CancellationToken ct = default(CancellationToken));

	Task<RefreshToken?> FindActiveRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken ct = default(CancellationToken));

	Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default(CancellationToken));

	Task RevokeRefreshTokenAsync(RefreshToken token, DateTime revokedAt, CancellationToken ct = default(CancellationToken));

	Task<Device?> FindDeviceByFingerprintAsync(string fingerprintHash, CancellationToken ct = default(CancellationToken));
}

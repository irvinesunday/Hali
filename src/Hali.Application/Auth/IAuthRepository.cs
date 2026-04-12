using System;
using System.Collections.Generic;
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

    Task UpdateExpoPushTokenAsync(Guid deviceId, string expoPushToken, CancellationToken ct = default(CancellationToken));

    Task<Account?> FindAccountByIdAsync(Guid accountId, CancellationToken ct = default(CancellationToken));

    Task UpdateAccountAsync(Account account, CancellationToken ct = default(CancellationToken));

    /// <summary>Returns expo push tokens for the given account IDs (one per account, latest device).</summary>
    Task<IReadOnlyList<string>> GetPushTokensByAccountIdsAsync(IEnumerable<Guid> accountIds, CancellationToken ct = default(CancellationToken));
}

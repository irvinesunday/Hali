using Hali.Domain.Entities.Auth;

namespace Hali.Application.Auth;

public interface IAuthRepository
{
    Task<Account?> FindAccountByPhoneAsync(string phoneE164, CancellationToken ct = default);
    Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default);
    Task SaveOtpChallengeAsync(OtpChallenge challenge, CancellationToken ct = default);
    Task<OtpChallenge?> FindActiveOtpAsync(string destination, DateTime now, CancellationToken ct = default);
    Task ConsumeOtpAsync(OtpChallenge challenge, DateTime consumedAt, CancellationToken ct = default);
    Task<Device> UpsertDeviceAsync(string fingerprintHash, Guid accountId, string? platform, string? appVersion, string? expoPushToken, DateTime now, CancellationToken ct = default);
    Task<RefreshToken?> FindActiveRefreshTokenAsync(string tokenHash, DateTime now, CancellationToken ct = default);
    Task SaveRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(RefreshToken token, DateTime revokedAt, CancellationToken ct = default);
    Task<Device?> FindDeviceByFingerprintAsync(string fingerprintHash, CancellationToken ct = default);
}

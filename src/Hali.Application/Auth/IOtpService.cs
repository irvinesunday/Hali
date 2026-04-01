using Hali.Domain.Enums;

namespace Hali.Application.Auth;

public interface IOtpService
{
    Task RequestOtpAsync(string destination, AuthMethod authMethod = AuthMethod.PhoneOtp, CancellationToken ct = default);

    /// <summary>Verifies and consumes the OTP. Returns false if invalid, expired, or already consumed.</summary>
    Task<bool> ConsumeOtpAsync(string destination, string otp, CancellationToken ct = default);
}

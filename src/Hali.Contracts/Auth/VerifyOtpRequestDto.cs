namespace Hali.Contracts.Auth;

public record VerifyOtpRequestDto(string Destination, string Otp, string DeviceFingerprintHash, string? Platform, string? AppVersion, string? ExpoPushToken);

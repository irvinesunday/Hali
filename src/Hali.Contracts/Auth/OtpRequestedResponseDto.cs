namespace Hali.Contracts.Auth;

/// <summary>
/// Success body for POST /v1/auth/otp. Carries a short human-readable
/// status message; clients should not depend on its exact wording.
/// </summary>
public sealed record OtpRequestedResponseDto(string Message);

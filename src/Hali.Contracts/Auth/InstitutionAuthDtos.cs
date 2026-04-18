using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Hali.Contracts.Auth;

// Requests -------------------------------------------------------------

public class MagicLinkRequestDto
{
    [Required]
    [MaxLength(254)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class MagicLinkVerifyRequestDto
{
    [Required]
    [MaxLength(256)]
    public string Token { get; set; } = string.Empty;
}

public class TotpConfirmRequestDto
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "code must be 6 digits")]
    public string Code { get; set; } = string.Empty;
}

public class TotpVerifyRequestDto
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "code must be 6 digits")]
    public string Code { get; set; } = string.Empty;
}

public class StepUpRequestDto
{
    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "code must be 6 digits")]
    public string Code { get; set; } = string.Empty;
}

// Responses ------------------------------------------------------------

public sealed record MagicLinkRequestResponseDto(
    string Message,
    DateTime ExpiresAt);

/// <summary>
/// Returned after a successful magic-link verify. A session cookie is
/// issued on the same response. The response body tells the client
/// which next step is expected before the session is considered
/// fully authenticated (step_up verified).
/// </summary>
public sealed record MagicLinkVerifyResponseDto(
    bool RequiresTotpEnrollment,
    bool RequiresTotpVerification,
    SessionEstablishedResponseDto Session);

public sealed record TotpEnrollResponseDto(
    string OtpAuthUri,
    IReadOnlyList<string> RecoveryCodes);

public sealed record SessionEstablishedResponseDto(
    DateTime CreatedAt,
    DateTime AbsoluteExpiresAt,
    int SoftWarningSeconds,
    int IdleTimeoutSeconds);

public sealed record StepUpResponseDto(
    DateTime VerifiedAt,
    int WindowSeconds);

public sealed record SessionRefreshedResponseDto(
    DateTime LastActivityAt,
    int SoftWarningSeconds,
    int IdleTimeoutSeconds);

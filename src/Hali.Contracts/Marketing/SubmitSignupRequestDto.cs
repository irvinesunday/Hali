using System.ComponentModel.DataAnnotations;

namespace Hali.Contracts.Marketing;

/// <summary>
/// Request body for POST /v1/marketing/signups.
/// Captures an early access signup from the public marketing site.
/// </summary>
public class SubmitSignupRequestDto
{
    [Required]
    [MaxLength(254)]
    [EmailAddress]
    public string Email { get; set; } = null!;
}

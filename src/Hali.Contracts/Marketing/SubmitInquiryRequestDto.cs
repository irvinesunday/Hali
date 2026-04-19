using System.ComponentModel.DataAnnotations;

namespace Hali.Contracts.Marketing;

/// <summary>
/// Request body for POST /v1/marketing/inquiries.
/// Captures an institution pilot inquiry from the public marketing site.
/// </summary>
public class SubmitInquiryRequestDto
{
    [Required]
    [MinLength(2)]
    [MaxLength(120)]
    public string Name { get; set; } = null!;

    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Organisation { get; set; } = null!;

    [Required]
    [MinLength(1)]
    [MaxLength(120)]
    public string Role { get; set; } = null!;

    [Required]
    [MaxLength(254)]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Area { get; set; } = null!;

    [Required]
    [RegularExpression("^(roads|water|electricity|transport|other)$",
        ErrorMessage = "Category must be one of: roads, water, electricity, transport, other.")]
    public string Category { get; set; } = null!;

    [MaxLength(500)]
    public string? Message { get; set; }
}

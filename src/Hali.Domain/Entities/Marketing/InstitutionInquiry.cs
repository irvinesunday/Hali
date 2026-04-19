using System;

namespace Hali.Domain.Entities.Marketing;

/// <summary>
/// Records an institution pilot inquiry from the marketing site.
/// Intentionally minimal — this is capture-edge data, not a core
/// domain entity.
/// </summary>
public class InstitutionInquiry
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Organisation { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Message { get; set; }

    public DateTime SubmittedAt { get; set; }
}

using System;

namespace Hali.Contracts.Advisories;

public record OfficialPostResponseDto(
    Guid Id,
    Guid InstitutionId,
    string Type,
    string Category,
    string Title,
    string Body,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string Status,
    Guid? RelatedClusterId,
    bool IsRestorationClaim,
    DateTime CreatedAt)
{
    /// <summary>
    /// Response status for <c>live_update</c> posts. Null for other post types.
    /// </summary>
    public string? ResponseStatus { get; init; }

    /// <summary>
    /// Severity for <c>scheduled_disruption</c> posts. Null for other post types.
    /// </summary>
    public string? Severity { get; init; }
}

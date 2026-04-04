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
    DateTime CreatedAt);

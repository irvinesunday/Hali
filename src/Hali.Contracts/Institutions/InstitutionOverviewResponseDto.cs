using System;
using System.Collections.Generic;

namespace Hali.Contracts.Institutions;

/// <summary>
/// Aggregate summary payload for the institution Overview page. Returns
/// scoped summary counts and the top-ranked areas in the caller's
/// institution scope. Activity items are served separately from
/// <c>GET /v1/institution/activity</c> so the two caches rotate
/// independently.
/// </summary>
public sealed record InstitutionOverviewResponseDto(
    InstitutionOverviewSummaryDto Summary,
    IReadOnlyList<InstitutionAreaDto> Areas);

public sealed record InstitutionOverviewSummaryDto(
    int ActiveSignals,
    int GrowingSignals,
    int UpdatesPostedToday,
    int StabilisedToday);

public sealed record InstitutionAreaDto(
    Guid Id,
    string Name,
    string Condition,
    int ActiveSignals,
    string? TopCategory,
    DateTime? LastUpdatedAt);

using System;
using System.Collections.Generic;

namespace Hali.Contracts.Institutions;

/// <summary>
/// Paginated list of signals in the caller's institution scope, filtered
/// by area and/or state. Uses cursor-based pagination per
/// <c>hali_institution_backend_contract_implications.md §5</c>.
/// </summary>
public sealed record InstitutionSignalsResponseDto(
    IReadOnlyList<InstitutionSignalListItemDto> Items,
    string? NextCursor);

public sealed record InstitutionSignalListItemDto(
    Guid Id,
    string Title,
    InstitutionAreaRefDto? Area,
    string Category,
    string Condition,
    string Trend,
    string? ResponseStatus,
    int AffectedCount,
    int RecentReports24h,
    long TimeActiveSeconds);

/// <summary>
/// Minimal area reference embedded in signal list items. Separate from
/// the full <see cref="InstitutionAreaDto"/> so the list response stays
/// lean — clients wanting full area state call
/// <c>GET /v1/institution/areas</c>.
/// </summary>
public sealed record InstitutionAreaRefDto(Guid Id, string Name);

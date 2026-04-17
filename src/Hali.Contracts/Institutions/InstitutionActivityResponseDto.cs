using System;
using System.Collections.Generic;

namespace Hali.Contracts.Institutions;

/// <summary>
/// Recent activity feed for the institution dashboard. Each item is a
/// state-change observation scoped to the institution's jurisdiction.
/// Cursor-based pagination per
/// <c>hali_institution_backend_contract_implications.md §5</c>.
/// </summary>
public sealed record InstitutionActivityResponseDto(
    IReadOnlyList<InstitutionActivityItemDto> Items,
    string? NextCursor);

public sealed record InstitutionActivityItemDto(
    Guid Id,
    string Type,
    string Message,
    DateTime Timestamp,
    Guid? SignalId);

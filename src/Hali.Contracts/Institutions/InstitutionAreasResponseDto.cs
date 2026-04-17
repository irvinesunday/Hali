using System.Collections.Generic;

namespace Hali.Contracts.Institutions;

/// <summary>
/// Full list of areas (institution jurisdictions) in the caller's scope.
/// Unlike the overview response, this list is not capped at 6 rows —
/// it returns every jurisdiction the institution owns so the Areas page
/// can render the complete set.
/// </summary>
public sealed record InstitutionAreasResponseDto(
    IReadOnlyList<InstitutionAreaDto> Items);

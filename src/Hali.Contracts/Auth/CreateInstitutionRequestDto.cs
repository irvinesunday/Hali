using System;
using System.Collections.Generic;

namespace Hali.Contracts.Auth;

public record CreateInstitutionRequestDto(
    string Name,
    List<Guid> JurisdictionLocalityIds,
    string? ContactEmail);

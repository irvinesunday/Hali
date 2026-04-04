using System;

namespace Hali.Contracts.Auth;

public record CreateInstitutionResponseDto(
    Guid InstitutionId,
    string InviteLink,
    DateTime InviteExpiresAt);

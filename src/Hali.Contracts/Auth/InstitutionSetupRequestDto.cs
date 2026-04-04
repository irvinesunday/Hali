namespace Hali.Contracts.Auth;

public record InstitutionSetupRequestDto(
    string InviteToken,
    string PhoneNumber);

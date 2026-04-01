namespace Hali.Contracts.Auth;

public record OtpRequestDto(
    string Destination,
    string AuthMethod
);

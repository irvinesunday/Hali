namespace Hali.Contracts.Auth;

public record TokenResponseDto(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);

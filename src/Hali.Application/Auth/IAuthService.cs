using Hali.Contracts.Auth;

namespace Hali.Application.Auth;

public interface IAuthService
{
    Task<TokenResponseDto> AuthenticateAsync(VerifyOtpRequestDto request, CancellationToken ct = default);
    Task<TokenResponseDto> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

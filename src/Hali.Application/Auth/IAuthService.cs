using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Auth;

namespace Hali.Application.Auth;

public interface IAuthService
{
    Task<TokenResponseDto> AuthenticateAsync(VerifyOtpRequestDto request, CancellationToken ct = default(CancellationToken));

    Task<TokenResponseDto> RefreshAsync(string refreshToken, CancellationToken ct = default(CancellationToken));

    Task RevokeAsync(string refreshToken, CancellationToken ct = default(CancellationToken));
}

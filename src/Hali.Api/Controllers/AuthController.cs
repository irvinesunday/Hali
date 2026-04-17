using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IOtpService _otpService;

    private readonly IAuthService _authService;

    private readonly IInstitutionService _institutionService;

    public AuthController(IOtpService otpService, IAuthService authService, IInstitutionService institutionService)
    {
        _otpService = otpService;
        _authService = authService;
        _institutionService = institutionService;
    }

    [HttpPost("otp")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestDto dto, CancellationToken ct)
    {
        await _otpService.RequestOtpAsync(dto.Destination, dto.AuthMethod, ct);
        return Ok(new OtpRequestedResponseDto("OTP sent"));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequestDto dto, CancellationToken ct)
    {
        return Ok(await _authService.AuthenticateAsync(dto, ct));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        return Ok(await _authService.RefreshAsync(dto.RefreshToken, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto dto, CancellationToken ct)
    {
        await _authService.RevokeAsync(dto.RefreshToken, ct);
        return NoContent();
    }

    [HttpPost("institution/setup")]
    public async Task<IActionResult> InstitutionSetup([FromBody] InstitutionSetupRequestDto dto, CancellationToken ct)
    {
        await _institutionService.SetupInstitutionAccountAsync(dto, ct);
        return Accepted();
    }
}

using Hali.Application.Auth;
using Hali.Contracts.Auth;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IOtpService _otpService;
    private readonly IAuthService _authService;

    public AuthController(IOtpService otpService, IAuthService authService)
    {
        _otpService = otpService;
        _authService = authService;
    }

    [HttpPost("otp")]
    public async Task<IActionResult> RequestOtp([FromBody] OtpRequestDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<AuthMethod>(dto.AuthMethod, ignoreCase: true, out var method))
            return BadRequest(new { error = "Invalid auth_method." });

        try
        {
            await _otpService.RequestOtpAsync(dto.Destination, method, ct);
            return Ok(new { message = "OTP sent" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_RATE_LIMITED")
        {
            return StatusCode(429, new { error = "Too many OTP requests. Please try again later." });
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequestDto dto, CancellationToken ct)
    {
        try
        {
            var tokens = await _authService.AuthenticateAsync(dto, ct);
            return Ok(tokens);
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_INVALID")
        {
            return BadRequest(new { error = "Invalid or expired OTP." });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        try
        {
            var tokens = await _authService.RefreshAsync(dto.RefreshToken, ct);
            return Ok(tokens);
        }
        catch (InvalidOperationException ex) when (ex.Message == "REFRESH_TOKEN_INVALID")
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto dto, CancellationToken ct)
    {
        await _authService.RevokeAsync(dto.RefreshToken, ct);
        return NoContent();
    }
}

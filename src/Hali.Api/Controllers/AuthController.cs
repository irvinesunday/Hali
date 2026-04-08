using System;
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
        try
        {
            await _otpService.RequestOtpAsync(dto.Destination, dto.AuthMethod, ct);
            return Ok(new
            {
                message = "OTP sent"
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_RATE_LIMITED")
        {
            return StatusCode(429, new
            {
                error = "Too many OTP requests. Please try again later."
            });
        }
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyOtpRequestDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.AuthenticateAsync(dto, ct));
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_INVALID")
        {
            return BadRequest(new
            {
                error = "Invalid or expired OTP."
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _authService.RefreshAsync(dto.RefreshToken, ct));
        }
        catch (InvalidOperationException ex) when (ex.Message == "REFRESH_TOKEN_INVALID")
        {
            return Unauthorized(new
            {
                error = "Invalid or expired refresh token."
            });
        }
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
        try
        {
            await _institutionService.SetupInstitutionAccountAsync(dto, ct);
            return Accepted();
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_INVALID")
        {
            return BadRequest(new { error = "Invalid invite token." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_EXPIRED")
        {
            return BadRequest(new { error = "Invite token has expired." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "INVITE_ALREADY_ACCEPTED")
        {
            return BadRequest(new { error = "Invite has already been accepted." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OTP_RATE_LIMITED")
        {
            return StatusCode(429, new { error = "Too many OTP requests. Please try again later." });
        }
    }
}

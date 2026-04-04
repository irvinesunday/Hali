using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/devices")]
public class DevicesController : ControllerBase
{
    private readonly IAuthRepository _auth;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IAuthRepository auth, ILogger<DevicesController> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    [HttpPost("push-token")]
    [Authorize]
    public async Task<IActionResult> RegisterPushToken(
        [FromBody] RegisterPushTokenRequestDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ExpoPushToken))
            return BadRequest(new { error = "expo_push_token is required." });

        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            return BadRequest(new { error = "device_hash is required." });

        var device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device == null)
            return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });

        var start = DateTime.UtcNow;
        await _auth.UpdateExpoPushTokenAsync(device.Id, dto.ExpoPushToken, ct);
        var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        _logger.LogInformation(
            "{eventName} correlationId={CorrelationId} deviceId={DeviceId} durationMs={DurationMs}",
            "device.push_token_registered", correlationId, device.Id, durationMs);

        return NoContent();
    }
}

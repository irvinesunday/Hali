using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Observability;
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
    private readonly PushNotificationsMetrics _metrics;

    public DevicesController(
        IAuthRepository auth,
        ILogger<DevicesController> logger,
        PushNotificationsMetrics metrics)
    {
        _auth = auth;
        _logger = logger;
        _metrics = metrics;
    }

    [HttpPost("push-token")]
    [Authorize]
    public async Task<IActionResult> RegisterPushToken(
        [FromBody] RegisterPushTokenRequestDto dto,
        CancellationToken ct)
    {
        var missing = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(dto.ExpoPushToken)) missing["expoPushToken"] = new[] { "expo_push_token is required" };
        if (string.IsNullOrWhiteSpace(dto.DeviceHash)) missing["deviceHash"] = new[] { "device_hash is required" };
        if (missing.Count > 0)
        {
            throw new ValidationException(
                "expo_push_token and device_hash are required.",
                code: ErrorCodes.DeviceMissingFields,
                fieldErrors: missing);
        }

        var device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device == null)
        {
            // Previously returned 422. Re-typed to NotFound (404) because
            // "no matching device" is semantically a missing resource. The
            // client is expected to re-register the device before retrying.
            throw new NotFoundException(
                code: ErrorCodes.DeviceNotFound,
                message: "Device not recognised.");
        }

        // Snapshot the pre-write token so the registration result tag
        // reflects the real state change (new / updated / unchanged) rather
        // than the post-write state. The comparison is ordinal because
        // Expo tokens are case-sensitive opaque strings.
        var previousToken = device.ExpoPushToken;
        var result = previousToken switch
        {
            null => PushNotificationsMetrics.ResultNew,
            _ when string.Equals(previousToken, dto.ExpoPushToken, StringComparison.Ordinal)
                => PushNotificationsMetrics.ResultUnchanged,
            _ => PushNotificationsMetrics.ResultUpdated,
        };

        var start = DateTime.UtcNow;
        await _auth.UpdateExpoPushTokenAsync(device.Id, dto.ExpoPushToken, ct);
        var durationMs = (DateTime.UtcNow - start).TotalMilliseconds;

        _metrics.PushTokenRegistrationsTotal.Add(
            1,
            new KeyValuePair<string, object?>(PushNotificationsMetrics.TagResult, result));

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        _logger.LogInformation(
            "{eventName} correlationId={CorrelationId} deviceId={DeviceId} durationMs={DurationMs}",
            "device.push_token_registered", correlationId, device.Id, durationMs);

        return NoContent();
    }
}

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Hali.Domain.Entities.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using StackExchange.Redis;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/signals")]
public class SignalsController : ControllerBase
{
    private readonly ISignalIngestionService _ingestion;

    private readonly IAuthRepository _auth;

    public SignalsController(ISignalIngestionService ingestion, IAuthRepository auth)
    {
        _ingestion = ingestion;
        _auth = auth;
    }

    [HttpPost("preview")]
    [AllowAnonymous]
    public async Task<IActionResult> Preview([FromBody] SignalPreviewRequestDto dto, CancellationToken ct, [FromServices] IConnectionMultiplexer redis)
    {
        // BLOCKING-4 fix: rate limit anonymous NLP previews (10/IP/10min)
        var _previewIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var _previewKey = $"rl:signal-preview:{_previewIp}";
        var _previewDb = redis.GetDatabase();
        var _previewCount = await _previewDb.StringIncrementAsync(_previewKey);
        if (_previewCount == 1) await _previewDb.KeyExpireAsync(_previewKey, TimeSpan.FromMinutes(10));
        if (_previewCount > 10)
            throw new RateLimitException("integrity.rate_limited", "Too many preview requests.");

        if (string.IsNullOrWhiteSpace(dto.FreeText))
        {
            throw new ValidationException("free_text is required.",
                code: "validation.failed",
                fieldErrors: new System.Collections.Generic.Dictionary<string, string[]>
                {
                    ["free_text"] = ["free_text is required."]
                });
        }

        return Ok(await _ingestion.PreviewAsync(dto, ct));
    }

    [HttpPost("submit")]
    [Authorize]
    public async Task<IActionResult> Submit([FromBody] SignalSubmitRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            throw new ValidationException("idempotency_key is required.",
                code: "validation.failed",
                fieldErrors: new System.Collections.Generic.Dictionary<string, string[]>
                {
                    ["idempotency_key"] = ["idempotency_key is required."]
                });
        }
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
        {
            throw new ValidationException("device_hash is required.",
                code: "validation.failed",
                fieldErrors: new System.Collections.Generic.Dictionary<string, string[]>
                {
                    ["device_hash"] = ["device_hash is required."]
                });
        }
        Guid? accountId = null;
        string sub = base.User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (Guid.TryParse(sub, out var parsed))
        {
            accountId = parsed;
        }
        Device device = ((!string.IsNullOrWhiteSpace(dto.DeviceHash)) ? (await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct)) : null);
        Device device2 = device;

        return Ok(await _ingestion.SubmitAsync(dto, accountId, device2?.Id, ct));
    }
}

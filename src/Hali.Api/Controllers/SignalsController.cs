using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
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
        if (_previewCount > 10) return StatusCode(429, new { code = "rate_limited", message = "Too many preview requests." });

        if (string.IsNullOrWhiteSpace(dto.FreeText))
        {
            return BadRequest(new
            {
                error = "free_text is required."
            });
        }
        try
        {
            return Ok(await _ingestion.PreviewAsync(dto, ct));
        }
        catch (InvalidOperationException ex) when (ex.Message == "NLP_EXTRACTION_FAILED")
        {
            return StatusCode(502, new
            {
                error = "NLP extraction service unavailable."
            });
        }
        catch (InvalidOperationException ex2) when (ex2.Message == "NLP_INVALID_CATEGORY")
        {
            return UnprocessableEntity(new
            {
                error = "NLP returned an unrecognised category."
            });
        }
    }

    [HttpPost("submit")]
    [Authorize]
    public async Task<IActionResult> Submit([FromBody] SignalSubmitRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            return BadRequest(new
            {
                error = "idempotency_key is required."
            });
        }
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
        {
            return BadRequest(new
            {
                error = "device_hash is required."
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
        try
        {
            return Ok(await _ingestion.SubmitAsync(dto, accountId, device2?.Id, ct));
        }
        catch (InvalidOperationException ex) when (ex.Message == "SIGNAL_DUPLICATE")
        {
            return Conflict(new
            {
                error = "Signal already submitted with this idempotency key."
            });
        }
        catch (InvalidOperationException ex2) when (ex2.Message == "SIGNAL_RATE_LIMITED")
        {
            return StatusCode(429, new
            {
                error = "Too many signals submitted. Please try again later."
            });
        }
        catch (InvalidOperationException ex3) when (ex3.Message == "SIGNAL_INVALID_CATEGORY")
        {
            return UnprocessableEntity(new
            {
                error = "Invalid category."
            });
        }
        catch (InvalidOperationException ex4) when (ex4.Message == "SIGNAL_MISSING_COORDINATES")
        {
            return BadRequest(new
            {
                error = "latitude and longitude are required."
            });
        }
        catch (InvalidOperationException ex5) when (ex5.Message == "SIGNAL_INVALID_COORDINATES")
        {
            return UnprocessableEntity(new
            {
                error = "Latitude must be between -90 and 90, longitude between -180 and 180."
            });
        }
        catch (InvalidOperationException ex6) when (ex6.Message == "SIGNAL_SPATIAL_DERIVATION_FAILED")
        {
            return UnprocessableEntity(new
            {
                error = "Unable to derive spatial cell from provided coordinates."
            });
        }
    }
}

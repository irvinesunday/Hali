using System.Security.Claims;
using Hali.Application.Signals;
using Hali.Contracts.Signals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/signals")]
public class SignalsController : ControllerBase
{
    private readonly ISignalIngestionService _ingestion;

    public SignalsController(ISignalIngestionService ingestion)
    {
        _ingestion = ingestion;
    }

    [HttpPost("preview")]
    [AllowAnonymous]
    public async Task<IActionResult> Preview([FromBody] SignalPreviewRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.FreeText))
            return BadRequest(new { error = "free_text is required." });

        try
        {
            var result = await _ingestion.PreviewAsync(dto, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "NLP_EXTRACTION_FAILED")
        {
            return StatusCode(502, new { error = "NLP extraction service unavailable." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "NLP_INVALID_CATEGORY")
        {
            return UnprocessableEntity(new { error = "NLP returned an unrecognised category." });
        }
    }

    [HttpPost("submit")]
    [Authorize]
    public async Task<IActionResult> Submit([FromBody] SignalSubmitRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            return BadRequest(new { error = "idempotency_key is required." });

        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            return BadRequest(new { error = "device_hash is required." });

        Guid? accountId = null;
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(sub, out var parsed))
            accountId = parsed;

        try
        {
            var result = await _ingestion.SubmitAsync(dto, accountId, deviceId: null, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "SIGNAL_DUPLICATE")
        {
            return Conflict(new { error = "Signal already submitted with this idempotency key." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "SIGNAL_RATE_LIMITED")
        {
            return StatusCode(429, new { error = "Too many signals submitted. Please try again later." });
        }
        catch (InvalidOperationException ex) when (ex.Message == "SIGNAL_INVALID_CATEGORY")
        {
            return UnprocessableEntity(new { error = "Invalid category." });
        }
    }
}

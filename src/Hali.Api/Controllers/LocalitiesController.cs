using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Application.Notifications;
using Hali.Contracts.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/localities")]
[Authorize]
public class LocalitiesController : ControllerBase
{
    private readonly IFollowService _follows;
    private readonly ILogger<LocalitiesController> _logger;

    public LocalitiesController(IFollowService follows, ILogger<LocalitiesController> logger)
    {
        _follows = follows;
        _logger = logger;
    }

    [HttpGet("followed")]
    public async Task<IActionResult> GetFollowed(CancellationToken ct)
    {
        var accountId = GetAccountId()
            ?? throw new UnauthorizedException();

        var follows = await _follows.GetFollowedAsync(accountId, ct);
        return Ok(new { localityIds = follows.Select(f => f.LocalityId) });
    }

    [HttpPut("followed")]
    public async Task<IActionResult> SetFollowed(
        [FromBody] FollowedLocalitiesRequestDto dto,
        CancellationToken ct)
    {
        var accountId = GetAccountId()
            ?? throw new UnauthorizedException();

        await _follows.SetFollowedAsync(accountId, dto.LocalityIds, ct);

        var correlationId = HttpContext.Items["CorrelationId"] as string;
        _logger.LogInformation(
            "{eventName} correlationId={CorrelationId} accountId={AccountId} count={Count}",
            "locality.follows_updated", correlationId, accountId, dto.LocalityIds.Count);

        return NoContent();
    }

    private Guid? GetAccountId()
    {
        var raw = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

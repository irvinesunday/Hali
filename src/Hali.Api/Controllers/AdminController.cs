using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IInstitutionService _institutionService;

    public AdminController(IInstitutionService institutionService)
    {
        _institutionService = institutionService;
    }

    [HttpPost("institutions")]
    public async Task<IActionResult> CreateInstitution(
        [FromBody] CreateInstitutionRequestDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "name is required." });

        Guid adminAccountId = Guid.Empty;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            adminAccountId = parsed;

        var result = await _institutionService.CreateInstitutionWithInviteAsync(adminAccountId, dto, ct);
        return StatusCode(201, result);
    }

    [HttpDelete("institutions/{id:guid}/access")]
    public async Task<IActionResult> RevokeAccess(Guid id, CancellationToken ct)
    {
        await _institutionService.RevokeInstitutionAccessAsync(id, ct);
        return Ok(new { });
    }
}

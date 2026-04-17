using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Application.Institutions;
using Hali.Contracts.Clusters;
using Hali.Contracts.Institutions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

/// <summary>
/// Institution operational dashboard read routes under
/// <c>/v1/institution/*</c>. Every action requires a JWT with
/// <c>role = "institution"</c>. The institution id is read from the
/// <c>institution_id</c> JWT claim — never from a query parameter or
/// route segment (<c>docs/arch/SECURITY_POSTURE.md</c> §2, binding
/// decision in <c>hali_institution_backend_contract_implications.md</c>
/// §10).
/// </summary>
[ApiController]
[Route("v1/institution")]
[Authorize(Roles = "institution")]
public class InstitutionController : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int ActivityDefaultLimit = 15;

    private readonly IInstitutionReadService _reads;

    public InstitutionController(IInstitutionReadService reads)
    {
        _reads = reads;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<InstitutionOverviewResponseDto>> GetOverview(
        [FromQuery] Guid? areaId,
        CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        InstitutionOverviewResponseDto dto = await _reads.GetOverviewAsync(institutionId, areaId, ct);
        return Ok(dto);
    }

    [HttpGet("signals")]
    public async Task<ActionResult<InstitutionSignalsResponseDto>> GetSignals(
        [FromQuery] Guid? areaId,
        [FromQuery] string? state,
        [FromQuery] string? cursor,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken ct = default)
    {
        Guid institutionId = ResolveInstitutionId();
        InstitutionSignalsResponseDto dto = await _reads.GetSignalsAsync(
            institutionId, areaId, state, cursor, limit, ct);
        return Ok(dto);
    }

    [HttpGet("signals/{clusterId:guid}")]
    public async Task<ActionResult<ClusterResponseDto>> GetSignalDetail(
        Guid clusterId,
        CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        ClusterResponseDto dto = await _reads.GetSignalDetailAsync(institutionId, clusterId, ct);
        return Ok(dto);
    }

    [HttpGet("areas")]
    public async Task<ActionResult<InstitutionAreasResponseDto>> GetAreas(CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        InstitutionAreasResponseDto dto = await _reads.GetAreasAsync(institutionId, ct);
        return Ok(dto);
    }

    [HttpGet("activity")]
    public async Task<ActionResult<InstitutionActivityResponseDto>> GetActivity(
        [FromQuery] Guid? areaId,
        [FromQuery] string? cursor,
        [FromQuery] int limit = ActivityDefaultLimit,
        CancellationToken ct = default)
    {
        Guid institutionId = ResolveInstitutionId();
        InstitutionActivityResponseDto dto = await _reads.GetActivityAsync(
            institutionId, areaId, cursor, limit, ct);
        return Ok(dto);
    }

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    /// <summary>
    /// Resolves the caller's institution id from the <c>institution_id</c>
    /// JWT claim. The claim is required on every institution route — a
    /// missing or malformed claim is treated as a 403 (the caller lacks
    /// institution identity) rather than a 400, matching the same posture
    /// <see cref="OfficialPostsController"/> uses on its write path.
    /// </summary>
    private Guid ResolveInstitutionId()
    {
        string? claim = User.FindFirstValue("institution_id");
        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id))
        {
            throw new ForbiddenException(
                code: ErrorCodes.AuthInstitutionIdMissing,
                message: "Institution identity required.");
        }
        return id;
    }
}

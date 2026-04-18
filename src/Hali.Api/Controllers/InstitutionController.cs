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
    private readonly IInstitutionAcknowledgeService _acks;

    public InstitutionController(
        IInstitutionReadService reads,
        IInstitutionAcknowledgeService acks)
    {
        _reads = reads;
        _acks = acks;
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

    // Route rename (#207): `/v1/institution/signals*` → `/v1/institution/clusters*`.
    // The public contract exposes SignalCluster objects, not raw SignalEvents;
    // the older `signals` segment was inconsistent with every other Phase 1
    // surface (citizen app, OpenAPI) and is retired. Internal DTO + service
    // names (InstitutionSignalsResponseDto, GetSignalsAsync) are intentionally
    // left unchanged to bound blast radius — only the wire path moved.
    [HttpGet("clusters")]
    public async Task<ActionResult<InstitutionSignalsResponseDto>> GetClusters(
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

    [HttpGet("clusters/{clusterId:guid}")]
    public async Task<ActionResult<ClusterResponseDto>> GetClusterDetail(
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

    /// <summary>
    /// Queue of clusters currently in <c>possible_restoration</c> inside
    /// the caller's scope, enriched with live restoration vote counts so
    /// the dashboard shows the same evidence the lifecycle engine
    /// evaluates (#207 Phase 4). Ordered ascending by
    /// <c>possible_restoration_at</c> so the clusters that have sat
    /// longest surface at the top.
    /// </summary>
    [HttpGet("restoration")]
    public async Task<ActionResult<InstitutionRestorationQueueResponseDto>> GetRestorationQueue(
        [FromQuery] Guid? areaId,
        CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        InstitutionRestorationQueueResponseDto dto = await _reads.GetRestorationQueueAsync(
            institutionId, areaId, ct);
        return Ok(dto);
    }

    /// <summary>
    /// Records an explicit institution acknowledgement on a cluster in
    /// scope (#207 Phase 4). Emits an <c>institution.action.recorded</c>
    /// outbox event. Passive view tracking is deliberately deferred;
    /// this endpoint only records explicit operator intent. Idempotent on
    /// <c>idempotencyKey</c> — resubmitting returns the existing record
    /// without a duplicate event.
    /// </summary>
    [HttpPost("clusters/{clusterId:guid}/acknowledge")]
    public async Task<ActionResult<InstitutionAcknowledgeResponseDto>> AcknowledgeCluster(
        Guid clusterId,
        [FromBody] InstitutionAcknowledgeRequestDto request,
        CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        InstitutionAcknowledgeResponseDto dto = await _acks.AcknowledgeAsync(
            institutionId, clusterId, request, ct);
        return Accepted(dto);
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

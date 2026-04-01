using System.Security.Claims;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Participation;
using Hali.Contracts.Clusters;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/clusters")]
public class ClustersController : ControllerBase
{
    private readonly IParticipationService _participation;
    private readonly IClusterRepository _clusters;
    private readonly IAuthRepository _auth;

    public ClustersController(
        IParticipationService participation,
        IClusterRepository clusters,
        IAuthRepository auth)
    {
        _participation = participation;
        _clusters = clusters;
        _auth = auth;
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCluster(Guid id, CancellationToken ct)
    {
        var cluster = await _clusters.GetClusterByIdAsync(id, ct);
        if (cluster is null)
            return NotFound(new { error = "Cluster not found." });

        return Ok(new ClusterResponseDto(
            Id: cluster.Id,
            State: cluster.State.ToString().ToLowerInvariant(),
            Category: cluster.Category.ToString().ToLowerInvariant(),
            SubcategorySlug: cluster.SubcategorySlug,
            Title: cluster.Title,
            Summary: cluster.Summary,
            AffectedCount: cluster.AffectedCount,
            ObservingCount: cluster.ObservingCount,
            CreatedAt: cluster.CreatedAt,
            UpdatedAt: cluster.UpdatedAt,
            ActivatedAt: cluster.ActivatedAt,
            PossibleRestorationAt: cluster.PossibleRestorationAt,
            ResolvedAt: cluster.ResolvedAt
        ));
    }

    [HttpPost("{id:guid}/participation")]
    [Authorize]
    public async Task<IActionResult> RecordParticipation(
        Guid id,
        [FromBody] ParticipationRequestDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            return BadRequest(new { error = "device_hash is required." });

        if (string.IsNullOrWhiteSpace(dto.Type))
            return BadRequest(new { error = "type is required." });

        if (!Enum.TryParse<ParticipationType>(dto.Type, ignoreCase: true, out var type)
            || type is not (ParticipationType.Affected or ParticipationType.Observing or ParticipationType.NoLongerAffected))
            return UnprocessableEntity(new { error = "Invalid participation type.", code = "invalid_participation_type" });

        var device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device is null)
            return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });

        Guid? accountId = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            accountId = parsed;

        await _participation.RecordParticipationAsync(id, device.Id, accountId, type, dto.IdempotencyKey, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/context")]
    [Authorize]
    public async Task<IActionResult> AddContext(
        Guid id,
        [FromBody] ContextRequestDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            return BadRequest(new { error = "device_hash is required." });

        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "text is required." });

        var device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device is null)
            return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });

        try
        {
            await _participation.AddContextAsync(id, device.Id, dto.Text, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message == "CONTEXT_REQUIRES_AFFECTED")
        {
            return UnprocessableEntity(new { error = "Context requires an active affected participation.", code = "context_requires_affected_participation" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "CONTEXT_EDIT_WINDOW_EXPIRED")
        {
            return UnprocessableEntity(new { error = "Context edit window has expired.", code = "context_edit_window_expired" });
        }
    }

    [HttpPost("{id:guid}/restoration-response")]
    [Authorize]
    public async Task<IActionResult> RecordRestorationResponse(
        Guid id,
        [FromBody] RestorationResponseRequestDto dto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            return BadRequest(new { error = "device_hash is required." });

        var validResponses = new HashSet<string> { "still_affected", "restored", "not_sure" };
        if (string.IsNullOrWhiteSpace(dto.Response) || !validResponses.Contains(dto.Response))
            return UnprocessableEntity(new { error = "Invalid response value.", code = "invalid_restoration_response" });

        var device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device is null)
            return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });

        Guid? accountId = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            accountId = parsed;

        await _participation.RecordRestorationResponseAsync(id, device.Id, accountId, dto.Response, ct);
        return NoContent();
    }
}

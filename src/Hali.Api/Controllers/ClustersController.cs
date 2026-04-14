using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Participation;
using Hali.Contracts.Clusters;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/clusters")]
public class ClustersController : ControllerBase
{
    private readonly IParticipationService _participation;
    private readonly IParticipationRepository _participationRepo;
    private readonly IClusterRepository _clusters;
    private readonly IAuthRepository _auth;
    private readonly IOfficialPostsService _officialPosts;
    private readonly CivisOptions _civisOptions;

    public ClustersController(
        IParticipationService participation,
        IParticipationRepository participationRepo,
        IClusterRepository clusters,
        IAuthRepository auth,
        IOfficialPostsService officialPosts,
        IOptions<CivisOptions> civisOptions)
    {
        _participation = participation;
        _participationRepo = participationRepo;
        _clusters = clusters;
        _auth = auth;
        _officialPosts = officialPosts;
        _civisOptions = civisOptions.Value;
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCluster(Guid id, CancellationToken ct)
    {
        SignalCluster cluster = await _clusters.GetClusterByIdAsync(id, ct);
        if (cluster == null)
        {
            throw new NotFoundException("cluster.not_found", "Cluster not found.");
        }
        var officialPosts = await _officialPosts.GetByClusterIdAsync(id, ct);

        // Per-caller participation snapshot — only populated for
        // authenticated callers. The mobile app gates "Add Further Context"
        // and the restoration response CTA on these flags.
        MyParticipationDto? myParticipation = null;
        if (User.Identity?.IsAuthenticated == true
            && Guid.TryParse(User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"), out var callerAccountId))
        {
            var current = await _participationRepo.GetMostRecentByAccountAsync(id, callerAccountId, ct);
            if (current != null)
            {
                var typeStr = JsonNamingPolicy.SnakeCaseLower.ConvertName(current.ParticipationType.ToString());
                var isAffected = current.ParticipationType == ParticipationType.Affected;
                var withinWindow = isAffected
                    && DateTime.UtcNow <= current.CreatedAt.AddMinutes(_civisOptions.ContextEditWindowMinutes);
                myParticipation = new MyParticipationDto(
                    typeStr,
                    current.CreatedAt,
                    CanAddContext: withinWindow,
                    CanRespondToRestoration: isAffected);
            }
        }

        var dto = new ClusterResponseDto(
            cluster.Id,
            JsonNamingPolicy.SnakeCaseLower.ConvertName(cluster.State.ToString()),
            JsonNamingPolicy.SnakeCaseLower.ConvertName(cluster.Category.ToString()),
            cluster.SubcategorySlug,
            cluster.Title,
            cluster.Summary,
            cluster.AffectedCount,
            cluster.ObservingCount,
            cluster.CreatedAt,
            cluster.UpdatedAt,
            cluster.ActivatedAt,
            cluster.PossibleRestorationAt,
            cluster.ResolvedAt)
        {
            LocationLabel = cluster.LocationLabelText,
            OfficialPosts = officialPosts,
            MyParticipation = myParticipation,
        };
        return Ok(dto);
    }

    [HttpPost("{id:guid}/participation")]
    [Authorize]
    public async Task<IActionResult> RecordParticipation(Guid id, [FromBody] ParticipationRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
        {
            throw new ValidationException("device_hash is required.",
                code: "validation.failed",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["device_hash"] = ["device_hash is required."]
                });
        }
        if (string.IsNullOrWhiteSpace(dto.Type))
        {
            throw new ValidationException("type is required.",
                code: "validation.failed",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["type"] = ["type is required."]
                });
        }
        bool flag = !Enum.TryParse<ParticipationType>(dto.Type, ignoreCase: true, out var type);
        bool flag2 = flag;
        if (!flag2)
        {
            bool flag3 = (uint)type <= 2u;
            flag2 = !flag3;
        }
        if (flag2)
        {
            throw new ValidationException("Invalid participation type.",
                code: "validation.invalid_participation_type",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["type"] = ["Invalid participation type."]
                });
        }
        Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device == null)
        {
            throw new ValidationException("Device not recognised.",
                code: "validation.device_not_found",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["device_hash"] = ["Device not recognised."]
                });
        }
        Guid? accountId = null;
        if (Guid.TryParse(User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"), out var parsed))
        {
            accountId = parsed;
        }
        await _participation.RecordParticipationAsync(id, device.Id, accountId, type, dto.IdempotencyKey, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/context")]
    [Authorize]
    public async Task<IActionResult> AddContext(Guid id, [FromBody] ContextRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
        {
            throw new ValidationException("device_hash is required.",
                code: "validation.failed",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["device_hash"] = ["device_hash is required."]
                });
        }
        if (string.IsNullOrWhiteSpace(dto.Text))
        {
            throw new ValidationException("text is required.",
                code: "validation.failed",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["text"] = ["text is required."]
                });
        }
        Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device == null)
        {
            throw new ValidationException("Device not recognised.",
                code: "validation.device_not_found",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["device_hash"] = ["Device not recognised."]
                });
        }

        await _participation.AddContextAsync(id, device.Id, dto.Text, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/restoration-response")]
    [Authorize]
    public async Task<IActionResult> RecordRestorationResponse(Guid id, [FromBody] RestorationResponseRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.DeviceHash))
        {
            throw new ValidationException("device_hash is required.",
                code: "validation.failed",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["device_hash"] = ["device_hash is required."]
                });
        }
        HashSet<string> validResponses = new HashSet<string> { "still_affected", "restored", "not_sure" };
        if (string.IsNullOrWhiteSpace(dto.Response) || !validResponses.Contains(dto.Response))
        {
            throw new ValidationException("Invalid response value.",
                code: "validation.invalid_restoration_response",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["response"] = ["Invalid response value. Must be one of: still_affected, restored, not_sure."]
                });
        }
        Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
        if (device == null)
        {
            throw new ValidationException("Device not recognised.",
                code: "validation.device_not_found",
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["device_hash"] = ["Device not recognised."]
                });
        }
        Guid? accountId = null;
        if (Guid.TryParse(User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"), out var parsed))
        {
            accountId = parsed;
        }
        await _participation.RecordRestorationResponseAsync(id, device.Id, accountId, dto.Response, ct);
        return NoContent();
    }
}

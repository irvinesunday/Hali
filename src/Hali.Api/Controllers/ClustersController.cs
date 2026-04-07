using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Auth;
using Hali.Application.Clusters;
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
			return NotFound(new { error = "Cluster not found." });
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
				var typeStr = ToSnakeCase(current.ParticipationType.ToString());
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
			cluster.State.ToString().ToLowerInvariant(),
			cluster.Category.ToString().ToLowerInvariant(),
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
			OfficialPosts = officialPosts,
			MyParticipation = myParticipation,
		};
		return Ok(dto);
	}

	// PascalCase enum name → snake_case_lower, matching the global
	// JsonStringEnumConverter naming policy used elsewhere in the API.
	private static string ToSnakeCase(string pascal)
	{
		var sb = new System.Text.StringBuilder(pascal.Length + 4);
		for (int i = 0; i < pascal.Length; i++)
		{
			char c = pascal[i];
			if (i > 0 && char.IsUpper(c)) sb.Append('_');
			sb.Append(char.ToLowerInvariant(c));
		}
		return sb.ToString();
	}

	[HttpPost("{id:guid}/participation")]
	[Authorize]
	public async Task<IActionResult> RecordParticipation(Guid id, [FromBody] ParticipationRequestDto dto, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(dto.DeviceHash))
		{
			return BadRequest(new { error = "device_hash is required." });
		}
		if (string.IsNullOrWhiteSpace(dto.Type))
		{
			return BadRequest(new { error = "type is required." });
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
			return UnprocessableEntity(new
			{
				error = "Invalid participation type.",
				code = "invalid_participation_type"
			});
		}
		Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
		if (device == null)
		{
			return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });
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
			return BadRequest(new { error = "device_hash is required." });
		}
		if (string.IsNullOrWhiteSpace(dto.Text))
		{
			return BadRequest(new { error = "text is required." });
		}
		Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
		if (device == null)
		{
			return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });
		}
		try
		{
			await _participation.AddContextAsync(id, device.Id, dto.Text, ct);
			return NoContent();
		}
		catch (InvalidOperationException ex) when (ex.Message == "CONTEXT_REQUIRES_AFFECTED")
		{
			return UnprocessableEntity(new
			{
				error = "Context requires an active affected participation.",
				code = "policy_blocked",
				reason = "context_requires_affected_participation"
			});
		}
		catch (InvalidOperationException ex2) when (ex2.Message == "CONTEXT_EDIT_WINDOW_EXPIRED")
		{
			return UnprocessableEntity(new
			{
				error = "Context edit window has expired.",
				code = "policy_blocked",
				reason = "context_edit_window_expired"
			});
		}
	}

	[HttpPost("{id:guid}/restoration-response")]
	[Authorize]
	public async Task<IActionResult> RecordRestorationResponse(Guid id, [FromBody] RestorationResponseRequestDto dto, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(dto.DeviceHash))
		{
			return BadRequest(new { error = "device_hash is required." });
		}
		HashSet<string> validResponses = new HashSet<string> { "still_affected", "restored", "not_sure" };
		if (string.IsNullOrWhiteSpace(dto.Response) || !validResponses.Contains(dto.Response))
		{
			return UnprocessableEntity(new { error = "Invalid response value.", code = "invalid_restoration_response" });
		}
		Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
		if (device == null)
		{
			return UnprocessableEntity(new { error = "Device not recognised.", code = "device_not_found" });
		}
		Guid? accountId = null;
		if (Guid.TryParse(User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"), out var parsed))
		{
			accountId = parsed;
		}
		try
		{
			await _participation.RecordRestorationResponseAsync(id, device.Id, accountId, dto.Response, ct);
			return NoContent();
		}
		catch (InvalidOperationException ex) when (ex.Message == "RESTORATION_REQUIRES_AFFECTED")
		{
			return UnprocessableEntity(new
			{
				error = "Restoration response requires an active affected participation.",
				code = "policy_blocked",
				reason = "restoration_requires_affected_participation"
			});
		}
	}
}

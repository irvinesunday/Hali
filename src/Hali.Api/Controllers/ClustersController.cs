using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Auth;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Institutions;
using Hali.Application.Observability;
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
    private readonly IInstitutionReadRepository _institutionRead;
    private readonly CivisOptions _civisOptions;
    private readonly ClustersMetrics? _metrics;

    public ClustersController(
        IParticipationService participation,
        IParticipationRepository participationRepo,
        IClusterRepository clusters,
        IAuthRepository auth,
        IOfficialPostsService officialPosts,
        IInstitutionReadRepository institutionRead,
        IOptions<CivisOptions> civisOptions,
        ClustersMetrics? metrics = null)
    {
        _participation = participation;
        _participationRepo = participationRepo;
        _clusters = clusters;
        _auth = auth;
        _officialPosts = officialPosts;
        _institutionRead = institutionRead;
        _civisOptions = civisOptions.Value;
        _metrics = metrics;
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCluster(Guid id, CancellationToken ct)
    {
        SignalCluster cluster = await _clusters.GetClusterByIdAsync(id, ct);
        if (cluster == null)
        {
            throw new NotFoundException(ErrorCodes.ClusterNotFound, "Cluster not found.");
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

        // Restoration progress snapshot — aggregate counts only, populated
        // exclusively when the cluster is in possible_restoration. Pure read:
        // we derive the ratio from a single atomic snapshot rather than
        // invoking EvaluateRestorationAsync (which mutates state and emits
        // outbox events — unsafe on a GET path). The snapshot guarantees
        // yesVotes <= totalResponses by construction (#143).
        int? restorationYesVotes = null;
        int? restorationTotalVotes = null;
        double? restorationRatio = null;
        if (cluster.State == SignalState.PossibleRestoration)
        {
            RestorationCountSnapshot snapshot = await _participationRepo.GetRestorationCountSnapshotAsync(id, ct);
            restorationYesVotes = snapshot.YesVotes;
            restorationTotalVotes = snapshot.TotalResponses;
            restorationRatio = snapshot.TotalResponses > 0
                ? (double)snapshot.YesVotes / snapshot.TotalResponses
                : (double?)null;
        }

        // Derive response_status from the existing officialPosts list already
        // fetched above — no extra DB round-trip needed because each
        // OfficialPostResponseDto now carries the per-post ResponseStatus.
        // GetByClusterIdAsync returns posts ordered newest-first, so the
        // first live_update with a non-null status is the latest one.
        string? responseStatus = officialPosts
            .FirstOrDefault(p => p.Type == "live_update" && !string.IsNullOrEmpty(p.ResponseStatus))
            ?.ResponseStatus;

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
            RestorationRatio = restorationRatio,
            RestorationYesVotes = restorationYesVotes,
            RestorationTotalVotes = restorationTotalVotes,
            ResponseStatus = responseStatus,
        };
        return Ok(dto);
    }

    [HttpPost("{id:guid}/participation")]
    [Authorize]
    public async Task<IActionResult> RecordParticipation(Guid id, [FromBody] ParticipationRequestDto dto, CancellationToken ct)
    {
        // action_type is "unknown" until the `type` field is successfully
        // parsed — validation errors before that point still emit, bucketed
        // as unknown/rejected_validation so operators see the full slice of
        // rejected traffic. outcome defaults to dependency_error and is
        // tightened by catch clauses on known exception kinds; the happy
        // path sets it to accepted just before the NoContent return. True
        // caller cancellation (ct.IsCancellationRequested at catch time)
        // flips `emit` to false so client disconnects do not skew the
        // taxonomy — mirrors the SignalsController pattern from #167.
        string actionType = ClustersMetrics.ActionTypeUnknown;
        string outcome = ClustersMetrics.OutcomeDependencyError;
        bool emit = true;
        try
        {
            if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("device_hash is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["device_hash"] = ["device_hash is required."]
                    });
            }
            if (string.IsNullOrWhiteSpace(dto.Type))
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("type is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["type"] = ["type is required."]
                    });
            }
            // Reject when the string is not a valid ParticipationType name
            // OR when the parsed value falls outside the three endpoint-
            // visible types (0–2). Values 3–5 (RestorationYes / RestorationNo
            // / RestorationUnsure) are persisted via /restoration-response,
            // not /participation, so accepting them here would let clients
            // bypass the server-side "requires affected" gate in
            // ParticipationService.
            if (!Enum.TryParse<ParticipationType>(dto.Type, ignoreCase: true, out var type) || (uint)type > 2u)
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("Invalid participation type.",
                    code: ErrorCodes.ValidationInvalidParticipationType,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["type"] = ["Invalid participation type."]
                    });
            }
            // Type parsed and in range — tag with the normalised value.
            actionType = ActionTypeFor(type);
            Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
            if (device == null)
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("Device not recognised.",
                    code: ErrorCodes.ValidationDeviceNotFound,
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
            outcome = ClustersMetrics.OutcomeAccepted;
            return NoContent();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            emit = false;
            throw;
        }
        catch (OperationCanceledException)
        {
            outcome = ClustersMetrics.OutcomeDependencyError;
            throw;
        }
        catch (ValidationException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (ConflictException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (RateLimitException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (DependencyException)
        {
            outcome = ClustersMetrics.OutcomeDependencyError;
            throw;
        }
        catch
        {
            throw;
        }
        finally
        {
            if (emit)
            {
                _metrics?.ParticipationActionsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(ClustersMetrics.TagActionType, actionType),
                    new KeyValuePair<string, object?>(ClustersMetrics.TagOutcome, outcome));
            }
        }
    }

    /// <summary>
    /// Maps the parsed <see cref="ParticipationType"/> to the
    /// <c>action_type</c> tag value used by
    /// <see cref="ClustersMetrics.ParticipationActionsTotal"/>. Only the
    /// three endpoint-visible participation types (0–2) are mapped — the
    /// restoration_* types (3–5) are persisted via
    /// <see cref="RecordRestorationResponse"/> and therefore never reach
    /// this endpoint, so their action_type is <c>restoration_response</c>
    /// (set at that endpoint's call site).
    /// </summary>
    private static string ActionTypeFor(ParticipationType type) => type switch
    {
        ParticipationType.Affected => ClustersMetrics.ActionTypeAffected,
        ParticipationType.Observing => ClustersMetrics.ActionTypeObserving,
        ParticipationType.NoLongerAffected => ClustersMetrics.ActionTypeNoLongerAffected,
        _ => ClustersMetrics.ActionTypeUnknown,
    };

    [HttpPost("{id:guid}/context")]
    [Authorize]
    public async Task<IActionResult> AddContext(Guid id, [FromBody] ContextRequestDto dto, CancellationToken ct)
    {
        // action_type is fixed by the route — this endpoint is only ever a
        // "context" action. ConflictException (context_requires_affected,
        // context_window_expired) is bucketed with other user-input
        // rejections; see ClustersMetrics.OutcomeRejectedValidation for the
        // full taxonomy.
        const string actionType = ClustersMetrics.ActionTypeContext;
        string outcome = ClustersMetrics.OutcomeDependencyError;
        bool emit = true;
        try
        {
            if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("device_hash is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["device_hash"] = ["device_hash is required."]
                    });
            }
            if (string.IsNullOrWhiteSpace(dto.Text))
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("text is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["text"] = ["text is required."]
                    });
            }
            Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
            if (device == null)
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("Device not recognised.",
                    code: ErrorCodes.ValidationDeviceNotFound,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["device_hash"] = ["Device not recognised."]
                    });
            }

            await _participation.AddContextAsync(id, device.Id, dto.Text, ct);
            outcome = ClustersMetrics.OutcomeAccepted;
            return NoContent();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            emit = false;
            throw;
        }
        catch (OperationCanceledException)
        {
            outcome = ClustersMetrics.OutcomeDependencyError;
            throw;
        }
        catch (ValidationException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (ConflictException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (DependencyException)
        {
            outcome = ClustersMetrics.OutcomeDependencyError;
            throw;
        }
        catch
        {
            throw;
        }
        finally
        {
            if (emit)
            {
                _metrics?.ParticipationActionsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(ClustersMetrics.TagActionType, actionType),
                    new KeyValuePair<string, object?>(ClustersMetrics.TagOutcome, outcome));
            }
        }
    }

    [HttpPost("{id:guid}/restoration-response")]
    [Authorize]
    public async Task<IActionResult> RecordRestorationResponse(Guid id, [FromBody] RestorationResponseRequestDto dto, CancellationToken ct)
    {
        // action_type is fixed by the route — every call to this endpoint
        // attempts a restoration_response regardless of the specific vote
        // value (restored / still_affected / not_sure). The vote value
        // deliberately does not become a tag: it's already one of three
        // bounded values, but making it a dimension would double this
        // instrument's series count with no operational lift (the
        // restoration-transition signal is better answered by
        // cluster_lifecycle_transitions_total).
        const string actionType = ClustersMetrics.ActionTypeRestorationResponse;
        string outcome = ClustersMetrics.OutcomeDependencyError;
        bool emit = true;
        try
        {
            if (string.IsNullOrWhiteSpace(dto.DeviceHash))
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("device_hash is required.",
                    code: ErrorCodes.ValidationMissingField,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["device_hash"] = ["device_hash is required."]
                    });
            }
            // Pattern-match avoids allocating a HashSet per request. The
            // three valid values are stable and part of the wire contract;
            // ParticipationService.RecordRestorationResponseAsync re-maps
            // the same strings to the RestorationYes / RestorationNo /
            // RestorationUnsure enum values.
            bool isValidResponse = dto.Response is "still_affected" or "restored" or "not_sure";
            if (string.IsNullOrWhiteSpace(dto.Response) || !isValidResponse)
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("Invalid response value.",
                    code: ErrorCodes.ValidationInvalidRestorationResponse,
                    fieldErrors: new Dictionary<string, string[]>
                    {
                        ["response"] = ["Invalid response value. Must be one of: still_affected, restored, not_sure."]
                    });
            }
            Device device = await _auth.FindDeviceByFingerprintAsync(dto.DeviceHash, ct);
            if (device == null)
            {
                outcome = ClustersMetrics.OutcomeRejectedValidation;
                throw new ValidationException("Device not recognised.",
                    code: ErrorCodes.ValidationDeviceNotFound,
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
            outcome = ClustersMetrics.OutcomeAccepted;
            return NoContent();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            emit = false;
            throw;
        }
        catch (OperationCanceledException)
        {
            outcome = ClustersMetrics.OutcomeDependencyError;
            throw;
        }
        catch (ValidationException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (ConflictException)
        {
            outcome = ClustersMetrics.OutcomeRejectedValidation;
            throw;
        }
        catch (DependencyException)
        {
            outcome = ClustersMetrics.OutcomeDependencyError;
            throw;
        }
        catch
        {
            throw;
        }
        finally
        {
            if (emit)
            {
                _metrics?.ParticipationActionsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(ClustersMetrics.TagActionType, actionType),
                    new KeyValuePair<string, object?>(ClustersMetrics.TagOutcome, outcome));
            }
        }
    }
}

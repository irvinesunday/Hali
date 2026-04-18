using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.InstitutionAdmin;
using Hali.Contracts.InstitutionAdmin;
using Hali.Domain.Entities.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hali.Api.Controllers;

/// <summary>
/// Phase 2 institution-admin routes (#196). Gated by
/// <c>[Authorize(Roles = "institution_admin")]</c>. Every operation is
/// scoped to the acting admin's institution — cross-institution targets
/// surface as 404 so a malicious admin cannot probe user IDs in other
/// institutions. Write endpoints (invite, role change) additionally
/// require a fresh step-up-auth verification via the #197 session
/// model: the session's <c>step_up_verified_at</c> must fall inside
/// the configured window (default 5 minutes).
/// </summary>
[ApiController]
[Route("v1/institution-admin")]
[Authorize(Roles = "institution_admin")]
public sealed class InstitutionAdminController : ControllerBase
{
    private readonly IInstitutionAdminService _service;
    private readonly InstitutionAuthOptions _authOpts;

    public InstitutionAdminController(
        IInstitutionAdminService service,
        IOptions<InstitutionAuthOptions> authOptions)
    {
        _service = service;
        _authOpts = authOptions.Value;
    }

    [HttpGet("users")]
    public async Task<ActionResult<InstitutionAdminUserListResponseDto>> ListUsers(CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        var result = await _service.ListUsersAsync(institutionId, ct);
        return Ok(result);
    }

    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<InstitutionAdminUserDetailResponseDto>> GetUser(Guid userId, CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        var result = await _service.GetUserAsync(institutionId, userId, ct);
        return Ok(result);
    }

    [HttpPost("users/invite")]
    public async Task<ActionResult<InviteInstitutionUserResponseDto>> InviteUser(
        [FromBody] InviteInstitutionUserRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }
        Guid institutionId = ResolveInstitutionId();
        Guid invitedById = ResolveAccountId();
        RequireFreshStepUp();
        var result = await _service.InviteUserAsync(institutionId, invitedById, dto, ct);
        return Ok(result);
    }

    [HttpPut("users/{userId:guid}/role")]
    public async Task<IActionResult> ChangeUserRole(
        Guid userId, [FromBody] ChangeUserRoleRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }
        Guid institutionId = ResolveInstitutionId();
        Guid actingAdminId = ResolveAccountId();
        RequireFreshStepUp();
        await _service.ChangeUserRoleAsync(institutionId, actingAdminId, userId, dto, ct);
        return NoContent();
    }

    [HttpGet("scope")]
    public async Task<ActionResult<InstitutionAdminScopeResponseDto>> GetScope(CancellationToken ct)
    {
        Guid institutionId = ResolveInstitutionId();
        var result = await _service.GetScopeAsync(institutionId, ct);
        return Ok(result);
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

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

    private Guid ResolveAccountId()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return id;
        }
        throw new UnauthorizedException(ErrorCodes.AuthUnauthorized, "Authentication required.");
    }

    /// <summary>
    /// Enforces that the active institution web session carries a
    /// <c>step_up_verified_at</c> timestamp inside the configured
    /// window (default 5 minutes). Throws <see cref="ForbiddenException"/>
    /// with <see cref="ErrorCodes.AuthStepUpRequired"/> otherwise.
    ///
    /// Bearer-JWT flows cannot satisfy step-up (the session + TOTP
    /// flow lives under the cookie surface), so bearer-authed
    /// institution_admin tokens reaching this path are rejected here —
    /// which is the intended behaviour. Write endpoints on the admin
    /// surface are cookie-only, gated by this helper.
    /// </summary>
    private void RequireFreshStepUp()
    {
        WebSession? session = HttpContext.Items["InstitutionWebSession"] as WebSession;
        if (session is null)
        {
            throw new ForbiddenException(
                code: ErrorCodes.AuthStepUpRequired,
                message: "Step-up verification required.");
        }
        if (session.StepUpVerifiedAt is null)
        {
            throw new ForbiddenException(
                code: ErrorCodes.AuthStepUpRequired,
                message: "Step-up verification required.");
        }
        // Reject future timestamps outright — a clock-skew / tampered
        // row with StepUpVerifiedAt in the future would pass a naive
        // (age < window) check because age is negative. Force the
        // client to re-verify when the stored timestamp is impossible.
        var now = DateTime.UtcNow;
        if (session.StepUpVerifiedAt.Value > now)
        {
            throw new ForbiddenException(
                code: ErrorCodes.AuthStepUpRequired,
                message: "Step-up verification required.");
        }
        var age = now - session.StepUpVerifiedAt.Value;
        if (age > TimeSpan.FromMinutes(_authOpts.StepUpWindowMinutes))
        {
            throw new ForbiddenException(
                code: ErrorCodes.AuthStepUpRequired,
                message: "Step-up verification required.");
        }
    }

    private ValidationException ValidationFromModelState()
    {
        var fieldErrors = new Dictionary<string, string[]>();
        foreach (var kv in ModelState)
        {
            if (kv.Value is not null && kv.Value.Errors.Count > 0)
            {
                fieldErrors[kv.Key] = kv.Value.Errors.Select(e => e.ErrorMessage).ToArray();
            }
        }
        return new ValidationException(
            "Request validation failed.",
            code: ErrorCodes.ValidationFailed,
            fieldErrors: fieldErrors);
    }
}

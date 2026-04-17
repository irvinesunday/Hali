using System;
using System.Collections.Generic;
using Hali.Api.FeatureFlags;
using Hali.Application.FeatureFlags;
using Hali.Contracts.FeatureFlags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/feature-flags")]
[AllowAnonymous]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _flags;
    private readonly IFeatureFlagContextFactory _contextFactory;

    public FeatureFlagsController(
        IFeatureFlagService flags,
        IFeatureFlagContextFactory contextFactory)
    {
        _flags = flags;
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns the resolved client-visible feature flags for the current
    /// request context. Anonymous callers are supported — their resolved
    /// values use <c>actor_type = "anonymous"</c>.
    ///
    /// Server-only flags are never included in the response, regardless
    /// of the caller's role.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResolvedFeatureFlagsResponseDto), StatusCodes.Status200OK)]
    public ActionResult<ResolvedFeatureFlagsResponseDto> GetResolvedFlags(
        [FromQuery(Name = "localityId")] Guid? localityId = null)
    {
        FeatureFlagEvaluationContext context = _contextFactory.FromHttpContext(HttpContext, localityId);
        IReadOnlyDictionary<string, bool> resolved = _flags.EvaluateClientVisible(context);

        return Ok(new ResolvedFeatureFlagsResponseDto { Flags = resolved });
    }
}

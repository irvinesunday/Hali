using System;
using System.Security.Claims;
using Hali.Application.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Hali.Api.FeatureFlags;

/// <summary>
/// Builds a <see cref="FeatureFlagEvaluationContext"/> from an incoming
/// HTTP request. Keeps claim extraction, environment resolution, and
/// optional locality wiring in one place so controllers don't have to
/// recompute it per call.
///
/// Anonymous callers get <c>actor_type = "anonymous"</c> and null
/// institution / locality. This is deliberate — client-visible flags
/// can target anonymous actors explicitly (e.g. a public landing page
/// dark launch).
/// </summary>
public interface IFeatureFlagContextFactory
{
    FeatureFlagEvaluationContext FromHttpContext(HttpContext http, Guid? localityId = null);
}

public sealed class FeatureFlagContextFactory : IFeatureFlagContextFactory
{
    private readonly IHostEnvironment _environment;

    public FeatureFlagContextFactory(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public FeatureFlagEvaluationContext FromHttpContext(HttpContext http, Guid? localityId = null)
    {
        ClaimsPrincipal user = http.User;
        string actorType = ResolveActorType(user);
        Guid? institutionId = ResolveInstitutionId(user);

        return new FeatureFlagEvaluationContext(
            Environment: _environment.EnvironmentName,
            InstitutionId: institutionId,
            LocalityId: localityId,
            ActorType: actorType);
    }

    private static string ResolveActorType(ClaimsPrincipal user)
    {
        if (user.Identity is null || !user.Identity.IsAuthenticated)
        {
            return "anonymous";
        }

        string? role = user.FindFirstValue(ClaimTypes.Role);
        return string.IsNullOrWhiteSpace(role) ? "anonymous" : role;
    }

    private static Guid? ResolveInstitutionId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirstValue("institution_id");
        return Guid.TryParse(raw, out Guid id) ? id : null;
    }
}

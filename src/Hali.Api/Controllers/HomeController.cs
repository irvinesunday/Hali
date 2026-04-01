using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Clusters;
using Hali.Application.Notifications;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/home")]
public class HomeController : ControllerBase
{
    private readonly IClusterRepository _clusters;
    private readonly IOfficialPostsService _officialPosts;
    private readonly IFollowService _follows;

    public HomeController(IClusterRepository clusters, IOfficialPostsService officialPosts, IFollowService follows)
    {
        _clusters = clusters;
        _officialPosts = officialPosts;
        _follows = follows;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetHome(CancellationToken ct)
    {
        // Determine followed localities for authenticated users
        var followedLocalityIds = new List<Guid>();
        if (User.Identity?.IsAuthenticated == true)
        {
            var raw = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (Guid.TryParse(raw, out var accountId))
            {
                var follows = await _follows.GetFollowedAsync(accountId, ct);
                followedLocalityIds = follows.Select(f => f.LocalityId).ToList();
            }
        }

        // Active clusters for followed wards
        IReadOnlyList<SignalCluster> followedActive = followedLocalityIds.Count > 0
            ? await _clusters.GetActiveByLocalitiesAsync(followedLocalityIds, ct)
            : new List<SignalCluster>();

        // All active clusters (for otherActiveSignals)
        var allActive = await _clusters.GetActiveClustersForDecayAsync(ct);
        var followedIds = new HashSet<Guid>(followedActive.Select(c => c.Id));

        // 4-section response
        var recurringAtThisTime = followedActive
            .Where(c => c.TemporalType == "recurring")
            .Select(ToDto).ToList();

        var activeNow = followedActive
            .Where(c => c.TemporalType != "recurring")
            .Select(ToDto).ToList();

        var otherActiveSignals = allActive
            .Where(c => !followedIds.Contains(c.Id))
            .Select(ToDto).ToList();

        var officialUpdates = new List<Hali.Contracts.Advisories.OfficialPostResponseDto>();
        foreach (var localityId in followedLocalityIds)
        {
            var posts = await _officialPosts.GetActiveByLocalityAsync(localityId, ct);
            officialUpdates.AddRange(posts);
        }

        return Ok(new HomeResponseDto
        {
            ActiveNow = activeNow,
            OfficialUpdates = officialUpdates,
            RecurringAtThisTime = recurringAtThisTime,
            OtherActiveSignals = otherActiveSignals
        });
    }

    private static ClusterResponseDto ToDto(SignalCluster c) =>
        new ClusterResponseDto(
            c.Id,
            c.State.ToString().ToLowerInvariant(),
            c.Category.ToString().ToLowerInvariant(),
            c.SubcategorySlug,
            c.Title,
            c.Summary,
            c.AffectedCount,
            c.ObservingCount,
            c.CreatedAt,
            c.UpdatedAt,
            c.ActivatedAt,
            c.PossibleRestorationAt,
            c.ResolvedAt);
}

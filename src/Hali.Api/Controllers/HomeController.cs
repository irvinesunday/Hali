using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Clusters;
using Hali.Contracts.Clusters;
using Hali.Contracts.Home;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/home")]
public class HomeController : ControllerBase
{
    private readonly IClusterRepository _clusters;
    private readonly IOfficialPostsService _officialPosts;

    public HomeController(IClusterRepository clusters, IOfficialPostsService officialPosts)
    {
        _clusters = clusters;
        _officialPosts = officialPosts;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetHome([FromQuery] Guid? localityId, CancellationToken ct)
    {
        var activeClusters = await _clusters.GetActiveClustersForDecayAsync(ct);

        var activeNow = activeClusters
            .Select(c => new ClusterResponseDto(
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
                c.ResolvedAt))
            .ToList();

        var officialUpdates = localityId.HasValue
            ? await _officialPosts.GetActiveByLocalityAsync(localityId.Value, ct)
            : new List<Contracts.Advisories.OfficialPostResponseDto>();

        return Ok(new HomeResponseDto
        {
            ActiveNow = activeNow,
            OfficialUpdates = officialUpdates
        });
    }
}

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Contracts.Advisories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

[ApiController]
[Route("v1/official-posts")]
public class OfficialPostsController : ControllerBase
{
    private readonly IOfficialPostsService _service;

    public OfficialPostsController(IOfficialPostsService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "institution")]
    public async Task<IActionResult> CreatePost([FromBody] CreateOfficialPostRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Category)
            || string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body))
        {
            return BadRequest(new { error = "type, category, title, and body are required." });
        }

        // In MVP, institution_id is carried via a custom claim or query param.
        // Parse from "institution_id" claim, fallback to X-Institution-Id header.
        Guid institutionId;
        var institutionClaim = User.FindFirstValue("institution_id");
        if (string.IsNullOrEmpty(institutionClaim))
            return Forbid(); // institution_id must come from JWT — no header fallback
        if (!Guid.TryParse(institutionClaim, out institutionId))
        {
            return UnprocessableEntity(new { error = "Institution identity required.", code = "institution_required" });
        }

        Guid? authorAccountId = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            authorAccountId = parsed;

        try
        {
            var result = await _service.CreatePostAsync(institutionId, authorAccountId, dto, ct);
            return Created($"/v1/official-posts/{result.Id}", result);
        }
        catch (ArgumentException ex) when (ex.Message == "INVALID_POST_TYPE")
        {
            return UnprocessableEntity(new { error = "Invalid post type.", code = "invalid_post_type" });
        }
        catch (ArgumentException ex) when (ex.Message == "INVALID_CATEGORY")
        {
            return UnprocessableEntity(new { error = "Invalid category.", code = "invalid_category" });
        }
        catch (InvalidOperationException ex) when (ex.Message == "OUTSIDE_JURISDICTION")
        {
            return StatusCode(403, new { error = "Post scope is outside institution jurisdiction.", code = "outside_jurisdiction" });
        }
    }
}

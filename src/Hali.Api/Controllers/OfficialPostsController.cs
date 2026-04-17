using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Errors;
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
        var missing = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(dto.Type)) missing["type"] = new[] { "type is required" };
        if (string.IsNullOrWhiteSpace(dto.Category)) missing["category"] = new[] { "category is required" };
        if (string.IsNullOrWhiteSpace(dto.Title)) missing["title"] = new[] { "title is required" };
        if (string.IsNullOrWhiteSpace(dto.Body)) missing["body"] = new[] { "body is required" };
        if (missing.Count > 0)
        {
            throw new ValidationException(
                "type, category, title, and body are required.",
                code: ErrorCodes.OfficialPostMissingFields,
                fieldErrors: missing);
        }

        // institution_id must come from the JWT — no header fallback. A missing
        // or malformed claim means the caller is not authorized to act on any
        // institution's behalf, so surface as Forbidden (403) rather than a
        // bare MVC Forbid() response (which can also trigger a re-auth challenge).
        var institutionClaim = User.FindFirstValue("institution_id");
        if (string.IsNullOrEmpty(institutionClaim) || !Guid.TryParse(institutionClaim, out var institutionId))
        {
            throw new ForbiddenException(
                code: ErrorCodes.AuthInstitutionIdMissing,
                message: "Institution identity required.");
        }

        Guid? authorAccountId = null;
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed))
            authorAccountId = parsed;

        var result = await _service.CreatePostAsync(institutionId, authorAccountId, dto, ct);
        return Created($"/v1/official-posts/{result.Id}", result);
    }
}

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Feedback;
using Hali.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

/// <summary>
/// POST /v1/feedback — anonymous in-app feedback capture.
/// No auth required. Returns 202 immediately; persistence happens inline via
/// <see cref="IFeedbackService"/>. Rate limiting is intentionally deferred
/// (see PR for #156 — not re-added to OpenAPI until a limiter is wired in).
/// </summary>
[ApiController]
[Route("v1/feedback")]
[AllowAnonymous]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedback;

    public FeedbackController(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken ct)
    {
        await _feedback.SubmitAsync(request, GetAccountId(), ct);
        return Accepted();
    }

    /// <summary>
    /// Returns the authenticated account id if the caller presented a valid
    /// bearer token, otherwise null. The endpoint is anonymous, so an
    /// unauthenticated caller is the normal path.
    /// </summary>
    private Guid? GetAccountId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

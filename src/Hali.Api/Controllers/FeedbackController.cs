using Hali.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

/// <summary>
/// POST /v1/feedback — anonymous in-app feedback capture.
/// No auth required. Full rate limiting and persistence in a later session.
/// </summary>
[ApiController]
[Route("v1/feedback")]
[AllowAnonymous]
public class FeedbackController : ControllerBase
{
    // TODO: inject IFeedbackService when implemented

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Submit([FromBody] SubmitFeedbackRequest request)
    {
        // TODO: persist to app_feedback table via IFeedbackService
        return Accepted();
    }
}

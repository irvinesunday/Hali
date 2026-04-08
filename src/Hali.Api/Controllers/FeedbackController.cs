using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

/// <summary>
/// POST /v1/feedback — anonymous in-app feedback capture.
/// Rate-limited by IP/session. No auth required.
/// Stub: returns 202 immediately. Full service implementation in a later session.
/// </summary>
[ApiController]
[Route("v1/feedback")]
[AllowAnonymous]
public class FeedbackController : ControllerBase
{
	[HttpPost]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
	public IActionResult Submit([FromBody] object payload)
	{
		// TODO: inject IFeedbackService and persist to app_feedback table
		return Accepted();
	}
}

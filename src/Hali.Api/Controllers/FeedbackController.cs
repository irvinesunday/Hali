using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Feedback;
using Hali.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

/// <summary>
/// POST /v1/feedback — anonymous in-app feedback capture.
/// No auth required. Persistence is synchronous via
/// <see cref="IFeedbackService"/>; the response is <c>202 Accepted</c> to
/// signal "recorded for later processing" semantics to the client, not
/// asynchronous storage.
///
/// The endpoint is rate-limited with the repo's canonical Redis limiter
/// (see <see cref="IRateLimiter"/>) to bound abuse of a public write path
/// (#169 / R4). Authenticated callers are throttled per-account; anonymous
/// callers fall back to a per-IP bucket so a single account cannot be
/// silenced by an anonymous neighbour and vice versa. Over-cap requests
/// surface as the canonical 429 via <see cref="RateLimitException"/>.
/// </summary>
[ApiController]
[Route("v1/feedback")]
[AllowAnonymous]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedback;
    private readonly IRateLimiter _rateLimiter;

    // Conservative per-identity cap. Real human feedback submission is
    // sub-minute pace at worst; 10/min leaves genuine users untouched while
    // bounding the blast radius of a scripted flood against an anonymous
    // write path. Aligns with the order-of-magnitude of the existing
    // places/localities search limits (30/min — those are read fan-outs to
    // an upstream geocoder, which is why they can be more permissive).
    private const int RateLimitMaxRequests = 10;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    public FeedbackController(IFeedbackService feedback, IRateLimiter rateLimiter)
    {
        _feedback = feedback;
        _rateLimiter = rateLimiter;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitFeedbackRequest request,
        CancellationToken ct)
    {
        var accountId = GetAccountId();
        var rateKey = BuildRateLimitKey(accountId);
        var allowed = await _rateLimiter.IsAllowedAsync(rateKey, RateLimitMaxRequests, RateLimitWindow, ct);
        if (!allowed)
        {
            throw new RateLimitException(
                code: ErrorCodes.RateLimitExceeded,
                message: "Too many feedback submissions. Please try again later.");
        }

        await _feedback.SubmitAsync(request, accountId, ct);
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

    /// <summary>
    /// Derives a rate-limit key from the caller's identity.
    /// Authenticated callers are keyed per-account so a flood from one
    /// account does not throttle unrelated callers sharing a NAT/proxy
    /// egress IP. Anonymous callers fall back to the remote IP — mirroring
    /// <c>PlacesController</c> and <c>LocalitiesController</c> — with the
    /// same <c>"unknown"</c> sentinel used by those sites when the remote
    /// address is absent (e.g. test server).
    /// </summary>
    private string BuildRateLimitKey(Guid? accountId)
    {
        if (accountId.HasValue)
        {
            return $"ratelimit:feedback_submit:acct:{accountId.Value}";
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ratelimit:feedback_submit:ip:{clientIp}";
    }
}

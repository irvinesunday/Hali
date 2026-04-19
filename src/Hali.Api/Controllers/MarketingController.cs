using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Application.Marketing;
using Hali.Contracts.Marketing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hali.Api.Controllers;

/// <summary>
/// Unauthenticated marketing capture endpoints.
/// POST /v1/marketing/signups — early access signup
/// POST /v1/marketing/inquiries — institution pilot inquiry
///
/// Both endpoints are public write paths and are therefore rate-limited
/// per remote IP using the repo's canonical Redis limiter
/// (<see cref="IRateLimiter"/>). Authenticated callers are treated the
/// same as anonymous callers here — there is no account-level bucket
/// because these endpoints are called from the public marketing site
/// before the caller has a Hali account.
/// </summary>
[ApiController]
[Route("v1/marketing")]
[AllowAnonymous]
public class MarketingController : ControllerBase
{
    private const int SignupRateLimitMax = 5;
    private const int InquiryRateLimitMax = 3;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);

    private readonly IMarketingService _marketing;
    private readonly IRateLimiter _rateLimiter;

    public MarketingController(IMarketingService marketing, IRateLimiter rateLimiter)
    {
        _marketing = marketing;
        _rateLimiter = rateLimiter;
    }

    [HttpPost("signups")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RecordSignup(
        [FromBody] SubmitSignupRequestDto request,
        CancellationToken ct)
    {
        string rateKey = BuildRateLimitKey("signup");
        bool allowed = await _rateLimiter.IsAllowedAsync(rateKey, SignupRateLimitMax, RateLimitWindow, ct);
        if (!allowed)
        {
            throw new RateLimitException(
                code: ErrorCodes.RateLimitExceeded,
                message: "Too many requests. Please try again later.");
        }

        await _marketing.RecordSignupAsync(request, ct);
        return Accepted();
    }

    [HttpPost("inquiries")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RecordInquiry(
        [FromBody] SubmitInquiryRequestDto request,
        CancellationToken ct)
    {
        string rateKey = BuildRateLimitKey("inquiry");
        bool allowed = await _rateLimiter.IsAllowedAsync(rateKey, InquiryRateLimitMax, RateLimitWindow, ct);
        if (!allowed)
        {
            throw new RateLimitException(
                code: ErrorCodes.RateLimitExceeded,
                message: "Too many requests. Please try again later.");
        }

        await _marketing.RecordInquiryAsync(request, ct);
        return Accepted();
    }

    private string BuildRateLimitKey(string action)
    {
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ratelimit:marketing_{action}:ip:{ip}";
    }
}

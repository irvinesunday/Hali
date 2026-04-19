using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Errors;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Application.Auth;

public sealed class MagicLinkService : IMagicLinkService
{
    private const int TokenByteLength = 32; // 256 bits
    // Max 3 magic link requests per email per 15-minute window.
    private const int MagicLinkRateMaxRequests = 3;
    private const int MagicLinkRateWindowMinutes = 15;

    private readonly IInstitutionAuthRepository _repo;
    private readonly IAuthRepository _authRepo;
    private readonly IInstitutionEmailSender _emailSender;
    private readonly IRateLimiter _rateLimiter;
    private readonly IAuthAuditService _audit;
    private readonly AuthOptions _authOpts;
    private readonly InstitutionAuthOptions _opts;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(
        IInstitutionAuthRepository repo,
        IAuthRepository authRepo,
        IInstitutionEmailSender emailSender,
        IRateLimiter rateLimiter,
        IAuthAuditService audit,
        IOptions<AuthOptions> authOptions,
        IOptions<InstitutionAuthOptions> options,
        ILogger<MagicLinkService> logger)
    {
        _repo = repo;
        _authRepo = authRepo;
        _emailSender = emailSender;
        _rateLimiter = rateLimiter;
        _audit = audit;
        _authOpts = authOptions.Value;
        _opts = options.Value;
        _logger = logger;
    }

    public async Task<MagicLinkIssued> IssueAsync(string email, string? ipAddress, CancellationToken ct)
    {
        string normalised = Normalise(email);
        DateTime now = DateTime.UtcNow;

        // Rate limit: 3 requests per email per 15 minutes.
        // Key uses the normalised email so case variants don't bypass the limit.
        string rateLimitKey = $"ratelimit:magic_link_issue:{normalised}";
        bool allowed = await _rateLimiter.IsAllowedAsync(
            rateLimitKey,
            MagicLinkRateMaxRequests,
            TimeSpan.FromMinutes(MagicLinkRateWindowMinutes),
            ct);

        if (!allowed)
        {
            await _audit.LogAsync(AuthAuditEvents.MagicLinkRateLimitHit, ipAddress, ct);
            _logger.LogWarning("institution_auth.magic_link.rate_limited");
            throw new RateLimitException(
                code: ErrorCodes.AuthMagicLinkRateLimited,
                message: "Too many magic link requests. Please wait before trying again.");
        }

        DateTime expiresAt = now.AddMinutes(_opts.MagicLinkTtlMinutes);

        string plaintext = GenerateTokenBase64Url();
        string hash = HashToken(plaintext);

        // Resolve the account now so verify can short-circuit the "email
        // unknown" case without a second DB round-trip. Unknown emails
        // still get a row written (with AccountId = null) so response
        // times don't disclose whether the email is registered.
        Domain.Entities.Auth.Account? account = await _authRepo.FindAccountByEmailAsync(normalised, ct);

        var token = new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            DestinationEmail = normalised,
            TokenHash = hash,
            AccountId = account?.Id,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            IpAddress = ipAddress,
        };
        await _repo.SaveMagicLinkAsync(token, ct);

        string url = BuildUrl(plaintext);
        // The URL is delivered via email — never logged. The outcome event
        // itself carries only the expiry timestamp; no email-derived
        // identifier is written to the structured log. CodeQL flagged an
        // earlier version that emitted a 4-byte SHA-256 fingerprint of the
        // email address — even though the fingerprint is one-way, the
        // scanner (correctly) treats derived data the same as raw PII.
        // Correlation across the issue/verify pair is available via the
        // per-request correlation id that CorrelationIdMiddleware already
        // tags on every log entry.
        await _emailSender.SendMagicLinkAsync(normalised, url, expiresAt, ct);
        await _audit.LogAsync(AuthAuditEvents.MagicLinkRequested, ipAddress, ct);
        _logger.LogInformation(
            "institution_auth.magic_link.issued expires_at={ExpiresAt}",
            expiresAt);

        return new MagicLinkIssued(plaintext, url, expiresAt);
    }

    public Task<MagicLinkToken?> ConsumeAsync(string plaintextToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
        {
            return Task.FromResult<MagicLinkToken?>(null);
        }
        string hash = HashToken(plaintextToken);
        return _repo.ConsumeMagicLinkAsync(hash, DateTime.UtcNow, ct);
    }

    public string HashToken(string plaintextToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    internal static string Normalise(string email)
        => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string GenerateTokenBase64Url()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private string BuildUrl(string plaintext)
    {
        // AppBaseUrl is the institution-web origin. The verify endpoint on
        // the web app accepts the token as a path segment; the web app
        // then POSTs it to /v1/auth/institution/magic-link/verify.
        string baseUrl = (_authOpts.AppBaseUrl ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/institution/magic-link?token={Uri.EscapeDataString(plaintext)}";
    }
}

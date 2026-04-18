using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Auth;
using Microsoft.Extensions.Options;

namespace Hali.Application.Auth;

public sealed class InstitutionSessionService : IInstitutionSessionService
{
    // 32 bytes of entropy = 256 bits — adequate for a session + CSRF token
    // that must survive brute force for the 12-hour absolute lifetime.
    private const int SessionTokenByteLength = 32;
    private const int CsrfTokenByteLength = 32;

    private readonly IInstitutionAuthRepository _repo;
    private readonly InstitutionAuthOptions _opts;

    public InstitutionSessionService(
        IInstitutionAuthRepository repo,
        IOptions<InstitutionAuthOptions> options)
    {
        _repo = repo;
        _opts = options.Value;
    }

    public async Task<SessionCreated> CreateAsync(Guid accountId, Guid? institutionId, string role, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        string sessionPlain = GenerateBase64UrlToken(SessionTokenByteLength);
        string csrfPlain = GenerateBase64UrlToken(CsrfTokenByteLength);

        var session = new WebSession
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            InstitutionId = institutionId,
            SessionTokenHash = HashSessionToken(sessionPlain),
            CsrfTokenHash = HashCsrfToken(csrfPlain),
            CreatedAt = now,
            LastActivityAt = now,
            AbsoluteExpiresAt = now.AddHours(_opts.SessionAbsoluteHours),
            Role = role,
        };

        await _repo.SaveWebSessionAsync(session, ct);
        return new SessionCreated(session, sessionPlain, csrfPlain);
    }

    public async Task<SessionValidation> ValidateAsync(string sessionTokenPlaintext, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionTokenPlaintext))
        {
            return new SessionValidation(SessionValidationResult.Invalid, null);
        }

        string hash = HashSessionToken(sessionTokenPlaintext);
        DateTime now = DateTime.UtcNow;
        WebSession? session = await _repo.FindActiveWebSessionAsync(hash, now, ct);
        if (session is null)
        {
            return new SessionValidation(SessionValidationResult.Invalid, null);
        }

        // Absolute expiry comes first — a session older than 12h cannot
        // be kept alive no matter how active it is.
        if (session.AbsoluteExpiresAt <= now)
        {
            return new SessionValidation(SessionValidationResult.AbsoluteTimeout, session);
        }

        TimeSpan idle = now - session.LastActivityAt;
        if (idle >= TimeSpan.FromMinutes(_opts.SessionIdleMinutes))
        {
            return new SessionValidation(SessionValidationResult.IdleTimeout, session);
        }

        return new SessionValidation(SessionValidationResult.Ok, session);
    }

    public Task TouchAsync(Guid sessionId, CancellationToken ct)
        => _repo.TouchWebSessionAsync(sessionId, DateTime.UtcNow, ct);

    public Task RevokeAsync(Guid sessionId, CancellationToken ct)
        => _repo.RevokeWebSessionAsync(sessionId, DateTime.UtcNow, ct);

    public Task MarkStepUpVerifiedAsync(Guid sessionId, CancellationToken ct)
        => _repo.MarkStepUpVerifiedAsync(sessionId, DateTime.UtcNow, ct);

    public string HashSessionToken(string plaintext) => Sha256Hex(plaintext);

    public string HashCsrfToken(string plaintext) => Sha256Hex(plaintext);

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    internal static string GenerateBase64UrlToken(int byteLength)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Sha256Hex(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Hali.Infrastructure.Auth;

/// <summary>
/// Development / test binding that does not dispatch email. Logs an
/// email-fingerprint + expiry so integration tests and local operators
/// can correlate a magic-link issuance without the URL ever touching
/// structured logs (per SECURITY_POSTURE.md §4).
/// </summary>
public sealed class NoOpInstitutionEmailSender : IInstitutionEmailSender
{
    private readonly ILogger<NoOpInstitutionEmailSender> _logger;

    public NoOpInstitutionEmailSender(ILogger<NoOpInstitutionEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendMagicLinkAsync(string destinationEmail, string url, DateTime expiresAt, CancellationToken ct)
    {
        _logger.LogInformation(
            "institution_auth.magic_link.dispatched email_fingerprint={Fingerprint} expires_at={ExpiresAt}",
            Fingerprint(destinationEmail), expiresAt);
        return Task.CompletedTask;
    }

    private static string Fingerprint(string email)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(email ?? string.Empty));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}

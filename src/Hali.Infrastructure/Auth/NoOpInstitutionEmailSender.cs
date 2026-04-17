using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Microsoft.Extensions.Logging;

namespace Hali.Infrastructure.Auth;

/// <summary>
/// Development / Testing binding that does not dispatch email. Logs the
/// outcome + expiry only — no email-derived identifier, no URL — so the
/// observability output stays PII-free per
/// <c>docs/arch/SECURITY_POSTURE.md §4</c>. A registered implementation
/// of this interface in a Production environment is a wiring mistake;
/// Program.cs only registers this binding when the environment is not
/// Production, and production deployments must provide their own
/// <see cref="IInstitutionEmailSender"/>.
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
        // Intentionally minimal: no destination, no URL. Issue/verify
        // correlation flows through the per-request correlation id that
        // CorrelationIdMiddleware already tags.
        _logger.LogInformation(
            "institution_auth.magic_link.noop_sender expires_at={ExpiresAt}",
            expiresAt);
        return Task.CompletedTask;
    }
}

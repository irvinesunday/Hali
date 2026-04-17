using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Auth;

/// <summary>
/// Pluggable email transport for institution-web auth emails. The
/// production binding will dispatch via the provider of record; the
/// default in-repo binding is a <c>NoOpInstitutionEmailSender</c> that
/// only logs the destination so local development never attempts a real
/// send.
/// </summary>
public interface IInstitutionEmailSender
{
    /// <summary>
    /// Delivers a magic-link email. The URL is opaque to the sender — it
    /// is the caller's responsibility to construct a safe, time-bounded
    /// link. Implementations MUST NOT log the URL.
    /// </summary>
    Task SendMagicLinkAsync(string destinationEmail, string url, DateTime expiresAt, CancellationToken ct);
}

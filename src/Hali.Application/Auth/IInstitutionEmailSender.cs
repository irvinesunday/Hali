using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Auth;

/// <summary>
/// Pluggable email transport for institution-web auth emails. A
/// production binding dispatches via the provider of record; the
/// in-repo <c>NoOpInstitutionEmailSender</c> logs only the outcome +
/// expiry timestamp and is wired exclusively in Development / Testing
/// so local runs never attempt a real send. Production deployments
/// must register their own implementation; the NoOp sender is NOT
/// registered when <c>IHostEnvironment.IsProduction()</c> is true.
/// </summary>
public interface IInstitutionEmailSender
{
    /// <summary>
    /// Delivers a magic-link email. The URL is opaque to the sender — it
    /// is the caller's responsibility to construct a safe, time-bounded
    /// link. Implementations MUST NOT log the URL and MUST NOT log the
    /// destination email or any identifier derived from it (CodeQL
    /// treats derived-from-PII sinks as PII exposure).
    /// </summary>
    Task SendMagicLinkAsync(string destinationEmail, string url, DateTime expiresAt, CancellationToken ct);
}

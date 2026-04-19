using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;

namespace Hali.Infrastructure.Auth;

/// <summary>
/// No-op audit service used until #251 (institution auth audit trail) is merged.
/// All writes are silently discarded. Hook point is preserved — replacing this
/// with a real implementation requires only a DI rebind.
/// </summary>
public sealed class NoOpAuthAuditService : IAuthAuditService
{
    public Task LogAsync(string eventType, string? ipAddress, CancellationToken ct = default)
        => Task.CompletedTask;
}

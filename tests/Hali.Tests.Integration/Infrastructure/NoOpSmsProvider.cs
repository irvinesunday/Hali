using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// SMS provider that silently discards all messages — used in integration tests
/// to prevent real SMS calls.
/// </summary>
internal sealed class NoOpSmsProvider : ISmsProvider
{
    public Task SendAsync(string destination, string message, CancellationToken ct = default)
        => Task.CompletedTask;
}

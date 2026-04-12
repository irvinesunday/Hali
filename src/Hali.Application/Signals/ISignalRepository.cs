using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Signals;

namespace Hali.Application.Signals;

public interface ISignalRepository
{
    Task<bool> IdempotencyKeyExistsAsync(string key, CancellationToken ct = default(CancellationToken));

    Task SetIdempotencyKeyAsync(string key, TimeSpan ttl, CancellationToken ct = default(CancellationToken));

    Task<bool> IsRateLimitAllowedAsync(string deviceHash, CancellationToken ct = default(CancellationToken));

    Task<string> BuildTaxonomyBlockAsync(CancellationToken ct = default(CancellationToken));

    Task<SignalEvent> PersistSignalAsync(SignalEvent signal, CancellationToken ct = default(CancellationToken));
}

using Hali.Domain.Entities.Signals;

namespace Hali.Application.Signals;

public interface ISignalRepository
{
    Task<bool> IdempotencyKeyExistsAsync(string key, CancellationToken ct = default);
    Task SetIdempotencyKeyAsync(string key, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> IsRateLimitAllowedAsync(string deviceHash, CancellationToken ct = default);

    /// <summary>
    /// Loads all active taxonomy categories and their subcategory slugs.
    /// Returns lines in format: "category: slug1, slug2"
    /// </summary>
    Task<string> BuildTaxonomyBlockAsync(CancellationToken ct = default);

    Task<SignalEvent> PersistSignalAsync(SignalEvent signal, CancellationToken ct = default);
}

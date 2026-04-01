using System.Text.Json;
using Hali.Application.Signals;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Infrastructure.Data.Signals;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Hali.Infrastructure.Signals;

public class SignalRepository : ISignalRepository
{
    private readonly SignalsDbContext _db;
    private readonly IDatabase _redis;

    // Rate limit: 10 signal submits per device per 10 minutes
    private const int RateLimitMax = 10;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(10);

    public SignalRepository(SignalsDbContext db, IDatabase redis)
    {
        _db = db;
        _redis = redis;
    }

    public async Task<bool> IdempotencyKeyExistsAsync(string key, CancellationToken ct = default)
    {
        return await _redis.KeyExistsAsync(key);
    }

    public async Task SetIdempotencyKeyAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        await _redis.StringSetAsync(key, "1", ttl, When.NotExists);
    }

    public async Task<bool> IsRateLimitAllowedAsync(string deviceHash, CancellationToken ct = default)
    {
        var key = $"rl:signal-submit:{deviceHash}";
        var count = await _redis.StringIncrementAsync(key);
        if (count == 1)
            await _redis.KeyExpireAsync(key, RateLimitWindow);
        return count <= RateLimitMax;
    }

    public async Task<string> BuildTaxonomyBlockAsync(CancellationToken ct = default)
    {
        var categories = await _db.TaxonomyCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Category)
            .ThenBy(c => c.SubcategorySlug)
            .ToListAsync(ct);

        if (!categories.Any())
            return GetFallbackTaxonomyBlock();

        var grouped = categories
            .GroupBy(c => c.Category.ToString().ToLowerInvariant())
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.SubcategorySlug))}");

        return string.Join("\n", grouped);
    }

    public async Task<SignalEvent> PersistSignalAsync(SignalEvent signal, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _db.SignalEvents.Add(signal);
        await _db.SaveChangesAsync(ct);

        var outbox = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            AggregateType = "SignalEvent",
            AggregateId = signal.Id,
            EventType = "signal_submitted",
            Payload = JsonSerializer.Serialize(new { signal_id = signal.Id, category = signal.Category.ToString().ToLowerInvariant() }),
            OccurredAt = DateTime.UtcNow
        };

        _db.OutboxEvents.Add(outbox);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return signal;
    }

    private static string GetFallbackTaxonomyBlock()
    {
        return """
            roads: potholes, flooding, obstruction, road_damage, impassable_section
            transport: matatu_obstruction, bus_stop_congestion, lane_blocking, access_disruption
            electricity: outage, unstable_supply, transformer_issue
            water: outage, low_pressure, burst_pipe, sewage_issue
            environment: illegal_dumping, waste_overflow, public_noise, pollution
            safety: exposed_hazard, broken_streetlight, unsafe_crossing
            governance: public_service_disruption, blocked_access_public_facility
            infrastructure: broken_drainage, damaged_footbridge, damaged_public_asset
            """;
    }
}

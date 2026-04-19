using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Observability;
using Hali.Application.Signals;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Infrastructure.Data.Signals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;

namespace Hali.Infrastructure.Signals;

public class SignalRepository : ISignalRepository
{
    private readonly SignalsDbContext _db;

    private readonly StackExchange.Redis.IDatabase _redis;

    private readonly ICorrelationContext? _correlationContext;

    private const int RateLimitMax = 10;

    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(10L);

    public SignalRepository(SignalsDbContext db, StackExchange.Redis.IDatabase redis, ICorrelationContext? correlationContext = null)
    {
        _db = db;
        _redis = redis;
        _correlationContext = correlationContext;
    }

    public async Task<bool> IdempotencyKeyExistsAsync(string key, CancellationToken ct = default(CancellationToken))
    {
        return await _redis.KeyExistsAsync(key);
    }

    public async Task SetIdempotencyKeyAsync(string key, TimeSpan ttl, CancellationToken ct = default(CancellationToken))
    {
        await _redis.StringSetAsync(key, "1", ttl, When.NotExists);
    }

    public async Task<bool> IsRateLimitAllowedAsync(string deviceHash, CancellationToken ct = default(CancellationToken))
    {
        string key = "rl:signal-submit:" + deviceHash;
        long count = await _redis.StringIncrementAsync(key, 1L);
        if (count == 1)
        {
            await _redis.KeyExpireAsync(key, RateLimitWindow);
        }
        return count <= RateLimitMax;
    }

    public async Task<string> BuildTaxonomyBlockAsync(CancellationToken ct = default(CancellationToken))
    {
        List<TaxonomyCategory> categories = await (from c in _db.TaxonomyCategories
                                                   where c.IsActive
                                                   orderby c.Category, c.SubcategorySlug
                                                   select c).ToListAsync(ct);
        if (!categories.Any())
        {
            return GetFallbackTaxonomyBlock();
        }
        IEnumerable<string> grouped = from c in categories
                                      group c by c.Category.ToString().ToLowerInvariant() into g
                                      select g.Key + ": " + string.Join(", ", g.Select((TaxonomyCategory x) => x.SubcategorySlug));
        return string.Join("\n", grouped);
    }

    public async Task<SignalEvent> PersistSignalAsync(SignalEvent signal, CancellationToken ct = default(CancellationToken))
    {
        await using (IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(ct))
        {
            _db.SignalEvents.Add(signal);
            await _db.SaveChangesAsync(ct);
            OutboxEvent outbox = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "signal_event",
                AggregateId = signal.Id,
                EventType = ObservabilityEvents.SignalSubmitted,
                SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                Payload = JsonSerializer.Serialize(new
                {
                    signal_id = signal.Id,
                    category = signal.Category.ToString().ToLowerInvariant()
                }),
                OccurredAt = DateTime.UtcNow,
                CorrelationId = _correlationContext?.CurrentCorrelationId ?? Guid.NewGuid(),
                CausationId = null,
            };
            _db.OutboxEvents.Add(outbox);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return signal;
    }

    private static string GetFallbackTaxonomyBlock()
    {
        return "roads: potholes, flooding, obstruction, road_damage, impassable_section\ntransport: matatu_obstruction, bus_stop_congestion, lane_blocking, access_disruption\nelectricity: outage, unstable_supply, transformer_issue\nwater: outage, low_pressure, burst_pipe, sewage_issue\nenvironment: illegal_dumping, waste_overflow, public_noise, pollution\nsafety: exposed_hazard, broken_streetlight, unsafe_crossing\ngovernance: public_service_disruption, blocked_access_public_facility\ninfrastructure: broken_drainage, damaged_footbridge, damaged_public_asset";
    }
}

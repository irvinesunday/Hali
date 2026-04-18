using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Institutions;
using StackExchange.Redis;

namespace Hali.Infrastructure.Institutions;

/// <summary>
/// Redis-backed idempotency store for institution acknowledge requests.
/// The key shape encodes institution + cluster + client key so two
/// institutions cannot collide on the same client-supplied idempotency
/// key. Encoded value is <c>"{acknowledgementId}|{recordedAtIso}"</c>.
/// TTL mirrors the signal-ingestion idempotency window — clients that
/// retry outside this horizon will get a fresh acknowledgement id (and
/// a fresh outbox event).
/// </summary>
public sealed class InstitutionAcknowledgementStore : IInstitutionAcknowledgementStore
{
    private static readonly TimeSpan ReplayTtl = TimeSpan.FromHours(24);

    private readonly IDatabase _redis;

    public InstitutionAcknowledgementStore(IDatabase redis)
    {
        _redis = redis;
    }

    public async Task<InstitutionAcknowledgementReplay?> TryGetReplayAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        CancellationToken ct)
    {
        string redisKey = BuildKey(institutionId, clusterId, idempotencyKey);
        RedisValue raw = await _redis.StringGetAsync(redisKey);
        if (!raw.HasValue)
        {
            return null;
        }
        string s = raw.ToString();
        int pipe = s.IndexOf('|');
        if (pipe <= 0)
        {
            return null;
        }
        if (!Guid.TryParse(s.AsSpan(0, pipe), out var ackId))
        {
            return null;
        }
        if (!DateTime.TryParse(
                s.AsSpan(pipe + 1),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var recordedAt))
        {
            return null;
        }
        return new InstitutionAcknowledgementReplay(ackId, recordedAt);
    }

    public async Task StoreAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        Guid acknowledgementId,
        DateTime recordedAt,
        CancellationToken ct)
    {
        string redisKey = BuildKey(institutionId, clusterId, idempotencyKey);
        string value = $"{acknowledgementId:D}|{recordedAt:O}";
        // When.NotExists: a concurrent writer that got there first wins,
        // and we leave their descriptor in place. The caller will re-read
        // on the next idempotent retry and see the winner's record.
        await _redis.StringSetAsync(redisKey, value, ReplayTtl, When.NotExists);
    }

    private static string BuildKey(Guid institutionId, Guid clusterId, string clientKey)
    {
        return $"idem:institution-ack:{institutionId:N}:{clusterId:N}:{clientKey}";
    }
}

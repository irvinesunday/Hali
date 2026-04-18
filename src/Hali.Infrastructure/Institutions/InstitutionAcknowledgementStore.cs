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

    public async Task<(InstitutionAcknowledgementReplay Winner, bool Claimed)> TryClaimAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        Guid candidateAcknowledgementId,
        DateTime candidateRecordedAt,
        CancellationToken ct)
    {
        string redisKey = BuildKey(institutionId, clusterId, idempotencyKey);
        string candidateValue = $"{candidateAcknowledgementId:D}|{candidateRecordedAt:O}";
        // SET NX under the same TTL the optimistic replay path observes.
        // The caller's candidate wins iff SET returns true; otherwise a
        // concurrent writer got there first and its descriptor is now the
        // authoritative record — we must return THAT descriptor so the
        // caller does not write a second outbox event under the same key.
        //
        // Edge case: SET NX returns false but a subsequent read finds no
        // descriptor (key expired or was flushed between the two calls).
        // We retry the SET NX a bounded number of times; only if the key
        // is genuinely held do we fall back to returning a non-claimed
        // descriptor so the caller can surface a retryable failure rather
        // than silently emitting a duplicate outbox event.
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            bool claimed = await _redis.StringSetAsync(
                redisKey, candidateValue, ReplayTtl, When.NotExists);
            if (claimed)
            {
                return (
                    new InstitutionAcknowledgementReplay(
                        candidateAcknowledgementId, candidateRecordedAt),
                    Claimed: true);
            }
            InstitutionAcknowledgementReplay? existing = await TryGetReplayAsync(
                institutionId, clusterId, idempotencyKey, ct);
            if (existing is not null)
            {
                return (existing, Claimed: false);
            }
            // SET NX reported the key existed, but by the time we read it
            // back it was gone. Loop and retry the claim; with TTL in the
            // hours range this realistically only happens under explicit
            // Redis flushes or malformed descriptor values.
        }
        // Exhausted retries: caller should treat as a transient conflict.
        return (
            new InstitutionAcknowledgementReplay(
                candidateAcknowledgementId, candidateRecordedAt),
            Claimed: false);
    }

    public async Task ReleaseClaimAsync(
        Guid institutionId,
        Guid clusterId,
        string idempotencyKey,
        CancellationToken ct)
    {
        string redisKey = BuildKey(institutionId, clusterId, idempotencyKey);
        await _redis.KeyDeleteAsync(redisKey);
    }

    private static string BuildKey(Guid institutionId, Guid clusterId, string clientKey)
    {
        return $"idem:institution-ack:{institutionId:N}:{clusterId:N}:{clientKey}";
    }
}

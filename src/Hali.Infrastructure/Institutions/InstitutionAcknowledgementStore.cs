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
        if (existing is null)
        {
            // Race edge case: SET NX reported the key existed, but by the
            // time we read it back the descriptor was gone (TTL on a very
            // short window, or an operator flush). Fall back to treating
            // the candidate as the winner — worst case the next retry
            // will still converge because the outbox event is also
            // idempotent on its own id.
            return (
                new InstitutionAcknowledgementReplay(
                    candidateAcknowledgementId, candidateRecordedAt),
                Claimed: true);
        }
        return (existing, Claimed: false);
    }

    private static string BuildKey(Guid institutionId, Guid clusterId, string clientKey)
    {
        return $"idem:institution-ack:{institutionId:N}:{clusterId:N}:{clientKey}";
    }
}

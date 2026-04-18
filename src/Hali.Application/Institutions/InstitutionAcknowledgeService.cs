using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Observability;
using Hali.Contracts.Institutions;
using Hali.Domain.Entities.Clusters;
using Microsoft.Extensions.Logging;

namespace Hali.Application.Institutions;

/// <summary>
/// Default <see cref="IInstitutionAcknowledgeService"/> implementation.
/// Scopes the acknowledgement to the caller's institution, enforces
/// idempotency via <see cref="IInstitutionAcknowledgementStore"/>, and
/// writes a single <c>institution.action.recorded</c> outbox row so the
/// action is auditable downstream. The acknowledgement itself does not
/// mutate cluster state — citizen-side lifecycle remains untouched.
/// </summary>
public sealed class InstitutionAcknowledgeService : IInstitutionAcknowledgeService
{
    private const int MaxNoteLength = 500;

    private readonly IInstitutionReadRepository _institutionRepo;
    private readonly IClusterRepository _clusterRepo;
    private readonly IInstitutionAcknowledgementStore _store;
    private readonly ILogger<InstitutionAcknowledgeService>? _logger;

    public InstitutionAcknowledgeService(
        IInstitutionReadRepository institutionRepo,
        IClusterRepository clusterRepo,
        IInstitutionAcknowledgementStore store,
        ILogger<InstitutionAcknowledgeService>? logger = null)
    {
        _institutionRepo = institutionRepo;
        _clusterRepo = clusterRepo;
        _store = store;
        _logger = logger;
    }

    public async Task<InstitutionAcknowledgeResponseDto> AcknowledgeAsync(
        Guid institutionId,
        Guid clusterId,
        InstitutionAcknowledgeRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ValidationException(
                "idempotencyKey is required.",
                code: ErrorCodes.InstitutionAcknowledgeMissingIdempotencyKey,
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["idempotencyKey"] = new[] { "idempotencyKey is required" },
                });
        }

        // Scope gate — cluster must be in the caller's jurisdiction. 404
        // (not 403) is deliberate so institution A cannot probe existence
        // of clusters owned by institution B. Same posture as the
        // GET /v1/institution/clusters/{id} route.
        IReadOnlyList<Guid> localityIds = await _institutionRepo.GetScopeLocalityIdsAsync(
            institutionId, areaId: null, ct);
        bool inScope = await _institutionRepo.IsClusterInScopeAsync(clusterId, localityIds, ct);
        if (!inScope)
        {
            throw new NotFoundException(
                ErrorCodes.InstitutionAcknowledgeOutOfScope,
                "Cluster not found.");
        }

        // Optimistic replay fast-path — most retries will hit this and
        // skip the Redis write entirely. This is not sufficient on its
        // own for correctness: two concurrent first-time submits can
        // both miss here, so the claim below is the actual source of
        // truth.
        InstitutionAcknowledgementReplay? replay = await _store.TryGetReplayAsync(
            institutionId, clusterId, request.IdempotencyKey, ct);
        if (replay is not null)
        {
            return new InstitutionAcknowledgeResponseDto(
                AcknowledgementId: replay.AcknowledgementId,
                ClusterId: clusterId,
                RecordedAt: replay.RecordedAt);
        }

        string? note = Truncate(request.Note, MaxNoteLength);
        // Deterministic acknowledgement id derived from the idempotency
        // triple. A retry after a mid-flight outbox write failure sees the
        // same id, so the DB PK on outbox_events.id acts as a second line
        // of defence against duplicate emission even if the Redis claim
        // release below is itself lost.
        Guid acknowledgementId = BuildDeterministicAckId(
            institutionId, clusterId, request.IdempotencyKey);
        DateTime now = DateTime.UtcNow;

        // Atomic claim BEFORE the outbox write closes the race Copilot
        // flagged on #279: the first writer wins the SET NX, any
        // concurrent caller that lost the race returns the winner's
        // descriptor without emitting a second outbox row.
        (InstitutionAcknowledgementReplay winner, bool claimed) = await _store.TryClaimAsync(
            institutionId, clusterId, request.IdempotencyKey,
            acknowledgementId, now, ct);

        if (!claimed)
        {
            return new InstitutionAcknowledgeResponseDto(
                AcknowledgementId: winner.AcknowledgementId,
                ClusterId: clusterId,
                RecordedAt: winner.RecordedAt);
        }

        OutboxEvent outbox = new OutboxEvent
        {
            Id = acknowledgementId,
            AggregateType = "signal_cluster",
            AggregateId = clusterId,
            EventType = ObservabilityEvents.InstitutionActionRecorded,
            SchemaVersion = ObservabilityEvents.SchemaVersionV1,
            Payload = JsonSerializer.Serialize(new
            {
                cluster_id = clusterId,
                institution_id = institutionId,
                action_type = "acknowledge",
                acknowledgement_id = acknowledgementId,
                idempotency_key = request.IdempotencyKey,
                note,
            }),
            OccurredAt = now,
        };

        try
        {
            await _clusterRepo.WriteOutboxEventAsync(outbox, ct);
        }
        catch
        {
            // Release the Redis claim so a subsequent retry does NOT hit
            // the replay fast-path and silently skip the outbox write.
            // Pair with the deterministic id above: if two retries race
            // past the release and both re-claim, the DB primary-key
            // constraint on outbox_events.id rejects the second insert.
            try
            {
                await _store.ReleaseClaimAsync(
                    institutionId, clusterId, request.IdempotencyKey, ct);
            }
            catch (Exception releaseEx)
            {
                _logger?.LogWarning(
                    releaseEx,
                    "Failed to release institution acknowledgement claim after outbox write error (cluster_id={ClusterId} institution_id={InstitutionId})",
                    clusterId,
                    institutionId);
            }
            throw;
        }

        _logger?.LogInformation(
            "{Event} cluster_id={ClusterId} institution_id={InstitutionId} acknowledgement_id={AckId}",
            ObservabilityEvents.InstitutionActionRecorded,
            clusterId,
            institutionId,
            acknowledgementId);

        return new InstitutionAcknowledgeResponseDto(
            AcknowledgementId: acknowledgementId,
            ClusterId: clusterId,
            RecordedAt: now);
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null)
        {
            return null;
        }
        return value.Length <= max ? value : value[..max];
    }

    private static Guid BuildDeterministicAckId(
        Guid institutionId, Guid clusterId, string idempotencyKey)
    {
        // Namespaced SHA-256 of the idempotency triple, truncated and
        // tagged with RFC 4122 variant + version-5 bits so the value is
        // a legal UUID that round-trips through PostgreSQL's uuid type.
        string canonical =
            $"hali.institution.ack|{institutionId:N}|{clusterId:N}|{idempotencyKey}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        byte[] bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}

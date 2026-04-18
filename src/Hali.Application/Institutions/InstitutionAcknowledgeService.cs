using System;
using System.Collections.Generic;
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
        Guid acknowledgementId = Guid.NewGuid();
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

        await _clusterRepo.WriteOutboxEventAsync(outbox, ct);

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
}

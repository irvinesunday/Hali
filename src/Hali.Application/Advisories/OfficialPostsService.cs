using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Institutions;
using Hali.Application.Observability;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Application.Advisories;

public class OfficialPostsService : IOfficialPostsService
{
    private readonly IOfficialPostRepository _repo;
    private readonly IClusterRepository _clusters;
    private readonly ClustersMetrics? _metrics;

    public OfficialPostsService(
        IOfficialPostRepository repo,
        IClusterRepository clusters,
        ClustersMetrics? metrics = null)
    {
        _repo = repo;
        _clusters = clusters;
        _metrics = metrics;
    }

    public async Task<OfficialPostResponseDto> CreatePostAsync(
        Guid institutionId,
        Guid? authorAccountId,
        CreateOfficialPostRequestDto dto,
        CancellationToken ct)
    {
        if (!Enum.TryParse<OfficialPostType>(dto.Type.Replace("_", ""), ignoreCase: true, out var postType))
            throw new ValidationException("Invalid post type.", code: ErrorCodes.OfficialPostInvalidType);

        if (!Enum.TryParse<CivicCategory>(dto.Category.Replace("_", ""), ignoreCase: true, out var category))
            throw new ValidationException("Invalid category.", code: ErrorCodes.OfficialPostInvalidCategory);

        // response_status is only valid on live_update posts; severity is only
        // valid on scheduled_disruption posts. Rejecting here keeps the data
        // model clean — we never persist a response_status on an advisory or
        // a severity on a live_update. Null on the "correct" type is fine.
        string? responseStatus = NormaliseOptional(dto.ResponseStatus);
        if (responseStatus is not null)
        {
            if (postType != OfficialPostType.LiveUpdate
                || !InstitutionVocabulary.ResponseStatuses.Contains(responseStatus))
            {
                throw new ValidationException(
                    "Invalid response_status.",
                    code: ErrorCodes.OfficialPostInvalidResponseStatus);
            }
        }

        string? severity = NormaliseOptional(dto.Severity);
        if (severity is not null)
        {
            if (postType != OfficialPostType.ScheduledDisruption
                || !InstitutionVocabulary.Severities.Contains(severity))
            {
                throw new ValidationException(
                    "Invalid severity.",
                    code: ErrorCodes.OfficialPostInvalidSeverity);
            }
        }

        // Geo-scope enforcement BEFORE insert — no out-of-jurisdiction row ever lands in the DB
        bool allowed = await _repo.CheckJurisdictionForLocalityAsync(institutionId, dto.LocalityId, ct);
        if (!allowed)
            throw new ForbiddenException(
                code: ErrorCodes.OfficialPostOutsideJurisdiction,
                message: "Post scope is outside institution jurisdiction.");

        var post = new OfficialPost
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            AuthorAccountId = authorAccountId,
            Type = postType,
            Category = category,
            Title = dto.Title,
            Body = dto.Body,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            Status = "published",
            RelatedClusterId = dto.RelatedClusterId,
            IsRestorationClaim = dto.IsRestorationClaim,
            ResponseStatus = responseStatus,
            Severity = severity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var scope = new OfficialPostScope
        {
            Id = Guid.NewGuid(),
            OfficialPostId = post.Id,
            LocalityId = dto.LocalityId,
            CorridorName = dto.CorridorName
        };

        var created = await _repo.CreateAsync(post, scope, ct);

        // Phase 11 trigger: live_update + is_restoration_claim → possible_restoration.
        // Atomic transition: cluster update + CivisDecision + outbox row commit together
        // (see issue #143 for the three-write atomicity rule; #207 canonical taxonomy).
        if (post.Type == OfficialPostType.LiveUpdate && post.IsRestorationClaim && post.RelatedClusterId.HasValue)
        {
            var cluster = await _clusters.GetClusterByIdAsync(post.RelatedClusterId.Value, ct);
            if (cluster != null && cluster.State == SignalState.Active)
            {
                DateTime now = DateTime.UtcNow;
                cluster.State = SignalState.PossibleRestoration;
                cluster.PossibleRestorationAt = now;
                cluster.UpdatedAt = now;
                CivisDecision decision = new CivisDecision
                {
                    Id = Guid.NewGuid(),
                    ClusterId = cluster.Id,
                    DecisionType = "possible_restoration",
                    ReasonCodes = JsonSerializer.Serialize(new[] { "institution_restoration_claim" }),
                    Metrics = JsonSerializer.Serialize(new { post_id = created.Id }),
                    CreatedAt = now
                };
                OutboxEvent outboxEvent = new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "signal_cluster",
                    AggregateId = cluster.Id,
                    EventType = ObservabilityEvents.ClusterPossibleRestoration,
                    SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                    Payload = JsonSerializer.Serialize(new
                    {
                        cluster_id = cluster.Id,
                        from = ClustersMetrics.StateActive,
                        to = ClustersMetrics.StatePossibleRestoration,
                        trigger = "institution_restoration_claim",
                        post_id = created.Id
                    }),
                    OccurredAt = now,
                    CorrelationId = Guid.NewGuid(),
                    CausationId = null,
                };
                await _clusters.ApplyClusterTransitionAsync(cluster, decision, outboxEvent, ct);

                _metrics?.ClusterLifecycleTransitionsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(ClustersMetrics.TagFromState, ClustersMetrics.StateActive),
                    new KeyValuePair<string, object?>(ClustersMetrics.TagToState, ClustersMetrics.StatePossibleRestoration));
            }
        }

        return MapToDto(created);
    }

    public async Task<List<OfficialPostResponseDto>> GetByClusterIdAsync(Guid clusterId, CancellationToken ct)
    {
        var posts = await _repo.GetByClusterIdAsync(clusterId, ct);
        return posts.Select(MapToDto).ToList();
    }

    public async Task<List<OfficialPostResponseDto>> GetActiveByLocalityAsync(Guid localityId, CancellationToken ct)
    {
        var posts = await _repo.GetActiveByLocalityAsync(localityId, ct);
        return posts.Select(MapToDto).ToList();
    }

    public async Task<List<OfficialPostResponseDto>> GetActiveByLocalitiesAsync(
        IEnumerable<Guid> localityIds, CancellationToken ct)
    {
        var posts = await _repo.GetActiveByLocalitiesAsync(localityIds, ct);
        return posts.Select(MapToDto).ToList();
    }

    private static string EnumToSnakeCase(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z])([A-Z])", "_$1").ToLowerInvariant();

    private static OfficialPostResponseDto MapToDto(OfficialPost p) => new OfficialPostResponseDto(
        p.Id,
        p.InstitutionId,
        EnumToSnakeCase(p.Type.ToString()),
        EnumToSnakeCase(p.Category.ToString()),
        p.Title,
        p.Body,
        p.StartsAt,
        p.EndsAt,
        p.Status,
        p.RelatedClusterId,
        p.IsRestorationClaim,
        p.CreatedAt)
    {
        ResponseStatus = p.ResponseStatus,
        Severity = p.Severity,
    };

    private static string? NormaliseOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Advisories;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Application.Advisories;

public class OfficialPostsService : IOfficialPostsService
{
    private readonly IOfficialPostRepository _repo;
    private readonly IClusterRepository _clusters;

    public OfficialPostsService(IOfficialPostRepository repo, IClusterRepository clusters)
    {
        _repo = repo;
        _clusters = clusters;
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

        // Phase 11 trigger: live_update + is_restoration_claim → possible_restoration
        if (post.Type == OfficialPostType.LiveUpdate && post.IsRestorationClaim && post.RelatedClusterId.HasValue)
        {
            var cluster = await _clusters.GetClusterByIdAsync(post.RelatedClusterId.Value, ct);
            if (cluster != null && cluster.State == SignalState.Active)
            {
                cluster.State = SignalState.PossibleRestoration;
                cluster.PossibleRestorationAt = DateTime.UtcNow;
                cluster.UpdatedAt = DateTime.UtcNow;
                await _clusters.UpdateClusterAsync(cluster, ct);
                await _clusters.WriteCivisDecisionAsync(new CivisDecision
                {
                    Id = Guid.NewGuid(),
                    ClusterId = cluster.Id,
                    DecisionType = "possible_restoration",
                    ReasonCodes = "[\"institution_restoration_claim\"]",
                    Metrics = $"{{\"post_id\":\"{created.Id}\"}}",
                    CreatedAt = DateTime.UtcNow
                }, ct);
                await _clusters.WriteOutboxEventAsync(new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "cluster",
                    AggregateId = cluster.Id,
                    EventType = "cluster_state_changed",
                    Payload = $"{{\"cluster_id\":\"{cluster.Id}\",\"from_state\":\"active\",\"to_state\":\"possible_restoration\",\"source\":\"institution_post\"}}",
                    OccurredAt = DateTime.UtcNow
                }, ct);
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
        p.CreatedAt);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Clusters;
using Hali.Application.Errors;
using Hali.Application.Participation;
using Hali.Contracts.Advisories;
using Hali.Contracts.Clusters;
using Hali.Contracts.Institutions;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Application.Institutions;

/// <summary>
/// Read-model service backing the institution operational dashboard.
/// Every read method is scoped by the caller's institution id (resolved
/// from the JWT by the controller); the service composes the repository
/// rows into the DTOs the UI consumes. Cursor pagination uses a
/// base64url-encoded "&lt;timestamp-iso-utc&gt;|&lt;id&gt;" tuple.
/// </summary>
public sealed class InstitutionReadService : IInstitutionReadService
{
    private const int OverviewAreaCap = 6;
    private const int MaxListLimit = 100;
    private const int DefaultListLimit = 20;

    private readonly IInstitutionReadRepository _repo;
    private readonly IClusterRepository _clusterRepo;
    private readonly IParticipationRepository _participationRepo;
    private readonly IOfficialPostsService _officialPosts;

    public InstitutionReadService(
        IInstitutionReadRepository repo,
        IClusterRepository clusterRepo,
        IParticipationRepository participationRepo,
        IOfficialPostsService officialPosts)
    {
        _repo = repo;
        _clusterRepo = clusterRepo;
        _participationRepo = participationRepo;
        _officialPosts = officialPosts;
    }

    public async Task<InstitutionOverviewResponseDto> GetOverviewAsync(
        Guid institutionId, Guid? areaId, CancellationToken ct)
    {
        IReadOnlyList<Guid> localityIds = await _repo.GetScopeLocalityIdsAsync(institutionId, areaId, ct);

        InstitutionSignalCounts counts = await _repo.GetSignalCountsAsync(
            localityIds, DateTime.UtcNow, ct);

        IReadOnlyList<InstitutionAreaRow> areaRows = await _repo.GetAreasAsync(institutionId, ct);
        List<InstitutionAreaDto> topAreas = areaRows
            .Take(OverviewAreaCap)
            .Select(ToAreaDto)
            .ToList();

        return new InstitutionOverviewResponseDto(
            Summary: new InstitutionOverviewSummaryDto(
                ActiveSignals: counts.ActiveSignals,
                GrowingSignals: counts.GrowingSignals,
                UpdatesPostedToday: counts.UpdatesPostedToday,
                StabilisedToday: counts.StabilisedToday),
            Areas: topAreas);
    }

    public async Task<InstitutionSignalsResponseDto> GetSignalsAsync(
        Guid institutionId, Guid? areaId, string? state, string? cursor, int limit,
        CancellationToken ct)
    {
        ValidateStateFilter(state);
        int effectiveLimit = NormaliseLimit(limit);

        IReadOnlyList<Guid> localityIds = await _repo.GetScopeLocalityIdsAsync(institutionId, areaId, ct);

        // When a specific area is requested, pass its locality to the query
        // so the list filter is applied inside the SQL rather than trimmed
        // post-hoc.
        Guid? areaLocalityId = areaId.HasValue && localityIds.Count > 0 ? localityIds[0] : (Guid?)null;

        (DateTime? cursorTs, Guid? cursorId) = DecodeCursor(cursor);

        // Request limit + 1 to detect "has more" without a second round-trip.
        IReadOnlyList<InstitutionClusterRow> rows = await _repo.ListClustersAsync(
            institutionId, localityIds, areaLocalityId, state, cursorTs, cursorId, effectiveLimit + 1, ct);

        bool hasMore = rows.Count > effectiveLimit;
        var page = hasMore ? rows.Take(effectiveLimit).ToList() : rows.ToList();

        // Batch-fetch enrichments (recent-reports, response-statuses) with
        // one round-trip each rather than per-row.
        List<Guid> pageIds = page.Select(r => r.Id).ToList();
        IReadOnlyDictionary<Guid, int> reportCounts = await _repo.GetRecentReportCountsAsync(
            pageIds, DateTime.UtcNow.AddHours(-24), ct);
        IReadOnlyDictionary<Guid, string> responseStatuses = await _repo.GetLatestResponseStatusesAsync(
            pageIds, ct);

        List<InstitutionSignalListItemDto> items = page
            .Select(row => new InstitutionSignalListItemDto(
                Id: row.Id,
                Title: row.Title ?? string.Empty,
                // AreaRef uses the jurisdiction id so this field stays
                // interchangeable with the `areaId` query parameter and with
                // /v1/institution/areas, which also key by jurisdiction id.
                // Rows without a matching jurisdiction (defensive fallback
                // for data mid-migration) surface no area ref.
                Area: row.AreaJurisdictionId.HasValue
                    ? new InstitutionAreaRefDto(row.AreaJurisdictionId.Value, row.LocalityDisplayName ?? string.Empty)
                    : null,
                Category: row.Category,
                Condition: DeriveCondition(row.State, row.AffectedCount),
                Trend: DeriveTrend(row.State, reportCounts.GetValueOrDefault(row.Id, 0)),
                ResponseStatus: responseStatuses.TryGetValue(row.Id, out var rs) ? rs : null,
                AffectedCount: row.AffectedCount,
                RecentReports24h: reportCounts.GetValueOrDefault(row.Id, 0),
                TimeActiveSeconds: ComputeTimeActiveSeconds(row.ActivatedAt ?? row.CreatedAt)))
            .ToList();

        string? nextCursor = hasMore && page.Count > 0
            ? EncodeCursor(page[^1].LastSeenAt, page[^1].Id)
            : null;

        return new InstitutionSignalsResponseDto(items, nextCursor);
    }

    public async Task<ClusterResponseDto> GetSignalDetailAsync(
        Guid institutionId, Guid clusterId, CancellationToken ct)
    {
        IReadOnlyList<Guid> localityIds = await _repo.GetScopeLocalityIdsAsync(institutionId, areaId: null, ct);

        // Scope gate — the cluster must be inside one of the institution's
        // jurisdiction localities. Returning a 404 (not a 403) denies the
        // existence probe so an attacker cannot confirm cluster IDs owned
        // by another institution.
        bool inScope = await _repo.IsClusterInScopeAsync(clusterId, localityIds, ct);
        if (!inScope)
        {
            throw new NotFoundException(ErrorCodes.ClusterNotFound, "Cluster not found.");
        }

        SignalCluster? cluster = await _clusterRepo.GetClusterByIdAsync(clusterId, ct);
        if (cluster is null)
        {
            throw new NotFoundException(ErrorCodes.ClusterNotFound, "Cluster not found.");
        }

        List<OfficialPostResponseDto> officialPosts = await _officialPosts.GetByClusterIdAsync(clusterId, ct);

        int? restorationYes = null;
        int? restorationTotal = null;
        double? restorationRatio = null;
        if (cluster.State == SignalState.PossibleRestoration)
        {
            RestorationCountSnapshot snapshot = await _participationRepo.GetRestorationCountSnapshotAsync(clusterId, ct);
            restorationYes = snapshot.YesVotes;
            restorationTotal = snapshot.TotalResponses;
            restorationRatio = snapshot.TotalResponses > 0
                ? (double)snapshot.YesVotes / snapshot.TotalResponses
                : (double?)null;
        }

        string? responseStatus = await _repo.GetLatestResponseStatusForClusterAsync(clusterId, ct);

        return new ClusterResponseDto(
            cluster.Id,
            ToSnakeCase(cluster.State.ToString()),
            ToSnakeCase(cluster.Category.ToString()),
            cluster.SubcategorySlug,
            cluster.Title,
            cluster.Summary,
            cluster.AffectedCount,
            cluster.ObservingCount,
            cluster.CreatedAt,
            cluster.UpdatedAt,
            cluster.ActivatedAt,
            cluster.PossibleRestorationAt,
            cluster.ResolvedAt)
        {
            LocationLabel = cluster.LocationLabelText,
            OfficialPosts = officialPosts,
            RestorationRatio = restorationRatio,
            RestorationYesVotes = restorationYes,
            RestorationTotalVotes = restorationTotal,
            ResponseStatus = responseStatus,
        };
    }

    public async Task<InstitutionAreasResponseDto> GetAreasAsync(
        Guid institutionId, CancellationToken ct)
    {
        IReadOnlyList<InstitutionAreaRow> rows = await _repo.GetAreasAsync(institutionId, ct);
        List<InstitutionAreaDto> items = rows.Select(ToAreaDto).ToList();
        return new InstitutionAreasResponseDto(items);
    }

    public async Task<InstitutionRestorationQueueResponseDto> GetRestorationQueueAsync(
        Guid institutionId, Guid? areaId, CancellationToken ct)
    {
        IReadOnlyList<Guid> localityIds = await _repo.GetScopeLocalityIdsAsync(institutionId, areaId, ct);
        if (localityIds.Count == 0)
        {
            return new InstitutionRestorationQueueResponseDto(Array.Empty<InstitutionRestorationQueueItemDto>());
        }

        IReadOnlyList<InstitutionRestorationRow> rows = await _repo.GetRestorationQueueAsync(localityIds, ct);

        // Bulk snapshot lookup avoids the N+1 query pattern Copilot
        // flagged on #207: one GroupBy over the queue's cluster ids,
        // then materialise zero-count snapshots for anything the query
        // didn't return.
        Guid[] rowClusterIds = rows.Select(r => r.ClusterId).ToArray();
        IReadOnlyDictionary<Guid, RestorationCountSnapshot> snapshots =
            await _participationRepo.GetRestorationCountSnapshotsAsync(rowClusterIds, ct);

        var items = new List<InstitutionRestorationQueueItemDto>(rows.Count);
        foreach (var row in rows)
        {
            RestorationCountSnapshot snapshot = snapshots.TryGetValue(row.ClusterId, out var s)
                ? s
                : new RestorationCountSnapshot(0, 0, 0);
            double? ratio = snapshot.TotalResponses > 0
                ? (double)snapshot.YesVotes / snapshot.TotalResponses
                : (double?)null;
            items.Add(new InstitutionRestorationQueueItemDto(
                ClusterId: row.ClusterId,
                Title: row.Title,
                Category: row.Category,
                LocalityId: row.LocalityId,
                LocalityName: row.LocalityName,
                PossibleRestorationAt: row.PossibleRestorationAt,
                RestorationYes: snapshot.YesVotes,
                StillAffected: snapshot.NoVotes,
                TotalRestorationResponses: snapshot.TotalResponses,
                RestorationRatio: ratio));
        }
        return new InstitutionRestorationQueueResponseDto(items);
    }

    public async Task<InstitutionActivityResponseDto> GetActivityAsync(
        Guid institutionId, Guid? areaId, string? cursor, int limit, CancellationToken ct)
    {
        int effectiveLimit = NormaliseLimit(limit);
        IReadOnlyList<Guid> localityIds = await _repo.GetScopeLocalityIdsAsync(institutionId, areaId, ct);
        Guid? areaLocalityId = areaId.HasValue && localityIds.Count > 0 ? localityIds[0] : (Guid?)null;

        (DateTime? cursorTs, Guid? cursorId) = DecodeCursor(cursor);

        IReadOnlyList<InstitutionActivityRow> rows = await _repo.GetActivityAsync(
            localityIds, areaLocalityId, cursorTs, cursorId, effectiveLimit + 1, ct);

        bool hasMore = rows.Count > effectiveLimit;
        var page = hasMore ? rows.Take(effectiveLimit).ToList() : rows.ToList();

        List<InstitutionActivityItemDto> items = page
            .Select(r => new InstitutionActivityItemDto(
                Id: r.Id,
                Type: r.Type,
                Message: r.Message,
                Timestamp: r.Timestamp,
                SignalId: r.SignalId))
            .ToList();

        string? nextCursor = hasMore && page.Count > 0
            ? EncodeCursor(page[^1].Timestamp, page[^1].Id)
            : null;

        return new InstitutionActivityResponseDto(items, nextCursor);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    public static InstitutionAreaDto ToAreaDto(InstitutionAreaRow row) => new(
        Id: row.Id,
        Name: row.DisplayName,
        Condition: ClassifyCondition(row.ActiveSignals),
        ActiveSignals: row.ActiveSignals,
        TopCategory: row.TopCategory,
        LastUpdatedAt: row.LastUpdatedAt);

    public static string ClassifyCondition(int activeSignals) => activeSignals switch
    {
        // Thresholds are intentionally simple — the UX treats condition as
        // a coarse indicator. Elevated = some activity, active = significant,
        // calm = none. Tunable centrally here so dashboards remain consistent.
        0 => InstitutionVocabulary.ConditionCalm,
        <= 2 => InstitutionVocabulary.ConditionElevated,
        _ => InstitutionVocabulary.ConditionActive,
    };

    public static string DeriveCondition(string state, int affectedCount)
    {
        return state switch
        {
            "active" => InstitutionVocabulary.ConditionActive,
            "possible_restoration" => InstitutionVocabulary.ConditionElevated,
            _ when affectedCount > 0 => InstitutionVocabulary.ConditionElevated,
            _ => InstitutionVocabulary.ConditionCalm,
        };
    }

    public static string DeriveTrend(string state, int recentReports24h)
    {
        if (state == "possible_restoration")
        {
            return InstitutionVocabulary.TrendPossibleRestoration;
        }
        return recentReports24h switch
        {
            0 => InstitutionVocabulary.TrendSlowing,
            < 5 => InstitutionVocabulary.TrendStable,
            _ => InstitutionVocabulary.TrendGrowing,
        };
    }

    public static long ComputeTimeActiveSeconds(DateTime activatedOrCreatedAt)
    {
        var delta = DateTime.UtcNow - activatedOrCreatedAt;
        return delta.Ticks > 0 ? (long)delta.TotalSeconds : 0L;
    }

    public static int NormaliseLimit(int requested)
    {
        if (requested <= 0)
        {
            return DefaultListLimit;
        }
        return Math.Min(requested, MaxListLimit);
    }

    public static void ValidateStateFilter(string? state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return;
        }
        if (!InstitutionVocabulary.SignalFilterStates.Contains(state))
        {
            throw new ValidationException(
                "Invalid state filter.",
                code: ErrorCodes.InstitutionInvalidStateFilter,
                fieldErrors: new Dictionary<string, string[]>
                {
                    ["state"] = new[]
                    {
                        $"state must be one of: {string.Join(", ", InstitutionVocabulary.SignalFilterStates)}",
                    },
                });
        }
    }

    public static string EncodeCursor(DateTime timestamp, Guid id)
    {
        var payload = $"{timestamp.ToUniversalTime():O}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static (DateTime? Timestamp, Guid? Id) DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return (null, null);
        }
        try
        {
            string padded = cursor.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            var bytes = Convert.FromBase64String(padded);
            var text = Encoding.UTF8.GetString(bytes);
            var sep = text.IndexOf('|');
            if (sep <= 0)
            {
                return (null, null);
            }
            if (!DateTime.TryParse(text[..sep], CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var ts))
            {
                return (null, null);
            }
            if (!Guid.TryParse(text[(sep + 1)..], out var id))
            {
                return (null, null);
            }
            return (ts, id);
        }
        catch (FormatException)
        {
            // Corrupt/malformed cursor — treat as "start from the top".
            return (null, null);
        }
    }

    public static string ToSnakeCase(string pascal)
    {
        // Mirrors ClustersController.ToSnakeCase — avoids ToLowerInvariant's
        // "possiblerestoration" trap flagged in COPILOT_LESSONS.md §5.
        if (string.IsNullOrEmpty(pascal)) return pascal;
        var sb = new StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

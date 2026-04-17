using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Institutions;
using Hali.Infrastructure.Data;
using Npgsql;

namespace Hali.Infrastructure.Institutions;

/// <summary>
/// Read-only repository backing the institution operational dashboard.
/// Uses the Advisories Npgsql connection pool (all modules share the
/// same physical database in the modular monolith), letting one
/// connection serve queries that span signal_clusters, official_posts,
/// and institution_jurisdictions — which are not reachable via a single
/// EF DbContext. Every query is parameterised; every list query is
/// bounded by the caller's institution locality set server-side.
/// </summary>
public sealed class InstitutionReadRepository : IInstitutionReadRepository
{
    private readonly HaliDataSources _dataSources;

    public InstitutionReadRepository(HaliDataSources dataSources)
    {
        _dataSources = dataSources;
    }

    public async Task<IReadOnlyList<Guid>> GetScopeLocalityIdsAsync(
        Guid institutionId, Guid? areaId, CancellationToken ct)
    {
        // When a specific area is requested, narrow the scope to that one
        // jurisdiction's locality — and also verify the jurisdiction belongs
        // to this institution in the same query, so an attacker cannot
        // elevate scope by supplying another institution's jurisdiction id.
        string sql = areaId.HasValue
            ? @"SELECT ij.locality_id FROM institution_jurisdictions ij
                WHERE ij.institution_id = @institutionId
                  AND ij.id = @areaId
                  AND ij.locality_id IS NOT NULL;"
            : @"SELECT ij.locality_id FROM institution_jurisdictions ij
                WHERE ij.institution_id = @institutionId
                  AND ij.locality_id IS NOT NULL;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("institutionId", institutionId);
        if (areaId.HasValue)
        {
            cmd.Parameters.AddWithValue("areaId", areaId.Value);
        }

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }

    public async Task<IReadOnlyList<InstitutionAreaRow>> GetAreasAsync(
        Guid institutionId, CancellationToken ct)
    {
        // Per jurisdiction row: active signal count (signal_clusters in the
        // jurisdiction's locality with state=active), top category (modal),
        // and last-updated (max last_seen_at). Corridor-only jurisdictions
        // (locality_id IS NULL) report zero activity since we do not yet
        // link clusters to corridors — they still appear in the list so
        // operators can see them on the Areas page.
        const string sql = @"
WITH jur AS (
    SELECT ij.id, ij.locality_id, ij.corridor_name
    FROM institution_jurisdictions ij
    WHERE ij.institution_id = @institutionId
),
active_counts AS (
    SELECT sc.locality_id, COUNT(*)::int AS cnt, MAX(sc.last_seen_at) AS last_updated
    FROM signal_clusters sc
    WHERE sc.state = 'active'
      AND sc.locality_id IN (SELECT locality_id FROM jur WHERE locality_id IS NOT NULL)
    GROUP BY sc.locality_id
),
ranked_cats AS (
    SELECT locality_id,
           category::text AS category,
           COUNT(*)::int AS c,
           ROW_NUMBER() OVER (PARTITION BY locality_id ORDER BY COUNT(*) DESC, category::text) AS rn
    FROM signal_clusters
    WHERE state = 'active'
      AND locality_id IN (SELECT locality_id FROM jur WHERE locality_id IS NOT NULL)
    GROUP BY locality_id, category
),
top_cat AS (
    SELECT locality_id, category FROM ranked_cats WHERE rn = 1
)
SELECT jur.id,
       jur.locality_id,
       jur.corridor_name,
       COALESCE(loc.ward_name, jur.corridor_name, 'Area') AS display_name,
       COALESCE(ac.cnt, 0) AS active_signals,
       tc.category AS top_category,
       ac.last_updated
FROM jur
LEFT JOIN localities loc ON loc.id = jur.locality_id
LEFT JOIN active_counts ac ON ac.locality_id = jur.locality_id
LEFT JOIN top_cat tc ON tc.locality_id = jur.locality_id
ORDER BY active_signals DESC, display_name ASC;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("institutionId", institutionId);

        var rows = new List<InstitutionAreaRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InstitutionAreaRow(
                Id: reader.GetGuid(0),
                LocalityId: reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1),
                CorridorName: reader.IsDBNull(2) ? null : reader.GetString(2),
                DisplayName: reader.GetString(3),
                ActiveSignals: reader.GetInt32(4),
                TopCategory: reader.IsDBNull(5) ? null : reader.GetString(5),
                LastUpdatedAt: reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<InstitutionClusterRow>> ListClustersAsync(
        IReadOnlyList<Guid> localityIds,
        Guid? areaLocalityId,
        string? filterState,
        DateTime? cursorLastSeenAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct)
    {
        if (localityIds.Count == 0)
        {
            return Array.Empty<InstitutionClusterRow>();
        }

        // State filter mapping — the institution list-view surfaces four
        // UX buckets; translate to the underlying signal_state values.
        //   active / growing  → state IN (unconfirmed, active)
        //   needs_attention   → state = active AND last_seen_at > now-6h
        //                       (the "recent activity spike" heuristic)
        //   restoration       → state = possible_restoration
        // When no filter is supplied, return any non-terminal state.
        var stateClause = filterState switch
        {
            "active" or "growing" => "sc.state IN ('unconfirmed','active')",
            "needs_attention" => "sc.state = 'active' AND sc.last_seen_at > (now() - interval '6 hours')",
            "restoration" => "sc.state = 'possible_restoration'",
            _ => "sc.state IN ('unconfirmed','active','possible_restoration')",
        };

        var cursorClause = cursorLastSeenAt.HasValue && cursorId.HasValue
            ? "AND (sc.last_seen_at, sc.id) < (@cursorTs, @cursorId)"
            : string.Empty;

        var areaClause = areaLocalityId.HasValue
            ? "AND sc.locality_id = @areaLocalityId"
            : string.Empty;

        var sql = $@"
SELECT sc.id,
       sc.title,
       sc.locality_id,
       loc.ward_name AS locality_display,
       sc.category::text AS category,
       sc.state::text AS state,
       sc.affected_count,
       sc.observing_count,
       sc.first_seen_at,
       sc.last_seen_at,
       sc.activated_at
FROM signal_clusters sc
LEFT JOIN localities loc ON loc.id = sc.locality_id
WHERE sc.locality_id = ANY(@localityIds)
  {areaClause}
  AND {stateClause}
  {cursorClause}
ORDER BY sc.last_seen_at DESC, sc.id DESC
LIMIT @limit;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("localityIds", (object)localityIds.ToArray());
        cmd.Parameters.AddWithValue("limit", limit);
        if (areaLocalityId.HasValue)
        {
            cmd.Parameters.AddWithValue("areaLocalityId", areaLocalityId.Value);
        }
        if (cursorLastSeenAt.HasValue && cursorId.HasValue)
        {
            cmd.Parameters.AddWithValue("cursorTs", cursorLastSeenAt.Value);
            cmd.Parameters.AddWithValue("cursorId", cursorId.Value);
        }

        var rows = new List<InstitutionClusterRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InstitutionClusterRow(
                Id: reader.GetGuid(0),
                Title: reader.IsDBNull(1) ? null : reader.GetString(1),
                LocalityId: reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2),
                LocalityDisplayName: reader.IsDBNull(3) ? null : reader.GetString(3),
                Category: reader.GetString(4),
                State: reader.GetString(5),
                AffectedCount: reader.GetInt32(6),
                ObservingCount: reader.GetInt32(7),
                CreatedAt: reader.GetDateTime(8),
                LastSeenAt: reader.GetDateTime(9),
                ActivatedAt: reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10)));
        }
        return rows;
    }

    public async Task<InstitutionSignalCounts> GetSignalCountsAsync(
        IReadOnlyList<Guid> localityIds, DateTime asOf, CancellationToken ct)
    {
        if (localityIds.Count == 0)
        {
            return new InstitutionSignalCounts(0, 0, 0, 0);
        }

        // All four counters run in a single DB round-trip. "Growing" is
        // defined as active clusters whose last_seen_at has moved inside
        // the last hour — the canonical "momentum" heuristic matching
        // the signal list filter. "StabilisedToday" is clusters resolved
        // or moved to possible_restoration since midnight UTC.
        DateTime startOfDayUtc = new DateTime(asOf.Year, asOf.Month, asOf.Day, 0, 0, 0, DateTimeKind.Utc);

        const string sql = @"
SELECT
  (SELECT COUNT(*)::int FROM signal_clusters
     WHERE locality_id = ANY(@lids) AND state = 'active') AS active_signals,
  (SELECT COUNT(*)::int FROM signal_clusters
     WHERE locality_id = ANY(@lids) AND state = 'active'
       AND last_seen_at > (@asOf - interval '1 hour')) AS growing_signals,
  (SELECT COUNT(*)::int FROM official_posts op
     INNER JOIN official_post_scopes ops ON ops.official_post_id = op.id
     WHERE ops.locality_id = ANY(@lids)
       AND op.status = 'published'
       AND op.created_at >= @startOfDayUtc) AS updates_posted_today,
  (SELECT COUNT(*)::int FROM signal_clusters
     WHERE locality_id = ANY(@lids)
       AND state IN ('resolved','possible_restoration')
       AND COALESCE(resolved_at, possible_restoration_at, last_seen_at) >= @startOfDayUtc) AS stabilised_today;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("lids", (object)localityIds.ToArray());
        cmd.Parameters.AddWithValue("asOf", asOf);
        cmd.Parameters.AddWithValue("startOfDayUtc", startOfDayUtc);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new InstitutionSignalCounts(
                ActiveSignals: reader.GetInt32(0),
                GrowingSignals: reader.GetInt32(1),
                UpdatesPostedToday: reader.GetInt32(2),
                StabilisedToday: reader.GetInt32(3));
        }
        return new InstitutionSignalCounts(0, 0, 0, 0);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetRecentReportCountsAsync(
        IReadOnlyList<Guid> clusterIds, DateTime since, CancellationToken ct)
    {
        if (clusterIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        const string sql = @"
SELECT cel.cluster_id, COUNT(*)::int AS report_count
FROM cluster_event_links cel
INNER JOIN signal_events se ON se.id = cel.signal_event_id
WHERE cel.cluster_id = ANY(@cids)
  AND se.occurred_at > @since
GROUP BY cel.cluster_id;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cids", (object)clusterIds.ToArray());
        cmd.Parameters.AddWithValue("since", since);

        var map = new Dictionary<Guid, int>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            map[reader.GetGuid(0)] = reader.GetInt32(1);
        }
        return map;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetLatestResponseStatusesAsync(
        IReadOnlyList<Guid> clusterIds, CancellationToken ct)
    {
        if (clusterIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Return the response_status from the newest live_update per cluster
        // that has a non-null response_status. Older rows (pre-migration) have
        // NULL and are skipped by DISTINCT ON semantics here.
        const string sql = @"
SELECT DISTINCT ON (op.related_cluster_id) op.related_cluster_id, op.response_status
FROM official_posts op
WHERE op.related_cluster_id = ANY(@cids)
  AND op.type = 'live_update'
  AND op.response_status IS NOT NULL
ORDER BY op.related_cluster_id, op.created_at DESC;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cids", (object)clusterIds.ToArray());

        var map = new Dictionary<Guid, string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            map[reader.GetGuid(0)] = reader.GetString(1);
        }
        return map;
    }

    public async Task<string?> GetLatestResponseStatusForClusterAsync(
        Guid clusterId, CancellationToken ct)
    {
        const string sql = @"
SELECT op.response_status
FROM official_posts op
WHERE op.related_cluster_id = @cid
  AND op.type = 'live_update'
  AND op.response_status IS NOT NULL
ORDER BY op.created_at DESC
LIMIT 1;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cid", clusterId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string s ? s : null;
    }

    public async Task<IReadOnlyList<InstitutionActivityRow>> GetActivityAsync(
        IReadOnlyList<Guid> localityIds,
        Guid? areaLocalityId,
        DateTime? cursorTimestamp,
        Guid? cursorId,
        int limit,
        CancellationToken ct)
    {
        if (localityIds.Count == 0)
        {
            return Array.Empty<InstitutionActivityRow>();
        }

        // Two event families compose the activity stream:
        //   1. Cluster lifecycle transitions from signal_clusters — mapped
        //      to new_signal / growing / stabilising / restoration / restored
        //      so operators see the whole civic flow on one feed.
        //   2. Official posts the institution has published — mapped to
        //      update_posted. These are scoped to localities the institution
        //      owns, so other institutions' posts never appear.
        //
        // Derived from the signal_clusters + official_posts tables directly
        // instead of outbox_events — outbox rows are transient and may be
        // published/purged. Reading the authoritative state tables gives a
        // durable activity feed even if outbox retention is tightened later.

        var localityFilter = areaLocalityId.HasValue
            ? "sc.locality_id = @areaLocalityId"
            : "sc.locality_id = ANY(@lids)";
        var postLocalityFilter = areaLocalityId.HasValue
            ? "ops.locality_id = @areaLocalityId"
            : "ops.locality_id = ANY(@lids)";

        var cursorClause = cursorTimestamp.HasValue && cursorId.HasValue
            ? "WHERE (timestamp, id) < (@cursorTs, @cursorId)"
            : string.Empty;

        var sql = $@"
WITH cluster_items AS (
    SELECT sc.id AS id,
           CASE sc.state::text
               WHEN 'active' THEN 'new_signal'
               WHEN 'possible_restoration' THEN 'restoration'
               WHEN 'resolved' THEN 'restored'
               ELSE 'growing'
           END AS type,
           COALESCE(sc.title, 'Signal in scope') AS message,
           COALESCE(sc.activated_at, sc.resolved_at, sc.possible_restoration_at, sc.last_seen_at) AS timestamp,
           sc.id AS signal_id
    FROM signal_clusters sc
    WHERE {localityFilter}
),
post_items AS (
    SELECT op.id AS id,
           'update_posted'::text AS type,
           op.title AS message,
           op.created_at AS timestamp,
           op.related_cluster_id AS signal_id
    FROM official_posts op
    INNER JOIN official_post_scopes ops ON ops.official_post_id = op.id
    WHERE {postLocalityFilter} AND op.status = 'published'
),
combined AS (
    SELECT * FROM cluster_items
    UNION ALL
    SELECT * FROM post_items
)
SELECT id, type, message, timestamp, signal_id
FROM combined
{cursorClause}
ORDER BY timestamp DESC, id DESC
LIMIT @limit;";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("lids", (object)localityIds.ToArray());
        cmd.Parameters.AddWithValue("limit", limit);
        if (areaLocalityId.HasValue)
        {
            cmd.Parameters.AddWithValue("areaLocalityId", areaLocalityId.Value);
        }
        if (cursorTimestamp.HasValue && cursorId.HasValue)
        {
            cmd.Parameters.AddWithValue("cursorTs", cursorTimestamp.Value);
            cmd.Parameters.AddWithValue("cursorId", cursorId.Value);
        }

        var rows = new List<InstitutionActivityRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new InstitutionActivityRow(
                Id: reader.GetGuid(0),
                Type: reader.GetString(1),
                Message: reader.GetString(2),
                Timestamp: reader.GetDateTime(3),
                SignalId: reader.IsDBNull(4) ? (Guid?)null : reader.GetGuid(4)));
        }
        return rows;
    }

    public async Task<bool> IsClusterInScopeAsync(
        Guid clusterId, IReadOnlyList<Guid> localityIds, CancellationToken ct)
    {
        if (localityIds.Count == 0)
        {
            return false;
        }

        const string sql = @"
SELECT EXISTS(
  SELECT 1 FROM signal_clusters sc
  WHERE sc.id = @cid AND sc.locality_id = ANY(@lids)
);";

        await using var conn = await _dataSources.Advisories.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("cid", clusterId);
        cmd.Parameters.AddWithValue("lids", (object)localityIds.ToArray());
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is bool b && b;
    }
}

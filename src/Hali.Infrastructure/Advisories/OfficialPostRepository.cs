using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Domain.Entities.Advisories;
using Hali.Infrastructure.Data.Advisories;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Advisories;

public class OfficialPostRepository : IOfficialPostRepository
{
    private readonly AdvisoriesDbContext _db;

    public OfficialPostRepository(AdvisoriesDbContext db)
    {
        _db = db;
    }

    public async Task<OfficialPost> CreateAsync(OfficialPost post, OfficialPostScope scope, CancellationToken ct)
    {
        _db.OfficialPosts.Add(post);
        _db.OfficialPostScopes.Add(scope);
        await _db.SaveChangesAsync(ct);
        return post;
    }

    public async Task<bool> CheckJurisdictionForLocalityAsync(Guid institutionId, Guid? localityId, CancellationToken ct)
    {
        bool hasJurisdiction = await _db.InstitutionJurisdictions
            .AnyAsync(j => j.InstitutionId == institutionId, ct);

        if (!hasJurisdiction)
            return true; // No jurisdiction rows → institution is unrestricted

        // Allow when: the institution has a jurisdiction row targeting this locality,
        // OR the jurisdiction row has no geometry constraint (geom IS NULL).
        var count = await _db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*)::int AS ""Value""
              FROM institution_jurisdictions ij
              WHERE ij.institution_id = {0}
                AND (ij.geom IS NULL OR ij.locality_id = {1})",
            institutionId, localityId).FirstOrDefaultAsync(ct);

        return count > 0;
    }

    public async Task<bool> JurisdictionIntersectsScopeAsync(Guid institutionId, Guid postId, CancellationToken ct)
    {
        // Use raw SQL with ST_Intersects to check geo-scope against institution jurisdiction
        // If either has no geometry (is null), we allow it (no spatial constraint set)
        var result = await _db.Database.ExecuteSqlRawAsync(
            @"SELECT 1 FROM official_post_scopes ops
              INNER JOIN institution_jurisdictions ij ON ij.institution_id = {0}
              WHERE ops.official_post_id = {1}
                AND (ops.geom IS NULL OR ij.geom IS NULL OR ST_Intersects(ops.geom, ij.geom))
              LIMIT 1",
            institutionId, postId);

        // If no jurisdiction rows exist for the institution, allow the post
        bool hasJurisdiction = await _db.InstitutionJurisdictions
            .AnyAsync(j => j.InstitutionId == institutionId, ct);

        if (!hasJurisdiction)
            return true;

        // Re-check with a scalar query
        var count = await _db.Database.SqlQueryRaw<int>(
            @"SELECT COUNT(*)::int AS ""Value"" FROM official_post_scopes ops
              INNER JOIN institution_jurisdictions ij ON ij.institution_id = {0}
              WHERE ops.official_post_id = {1}
                AND (ops.geom IS NULL OR ij.geom IS NULL OR ST_Intersects(ops.geom, ij.geom))",
            institutionId, postId).FirstOrDefaultAsync(ct);

        return count > 0;
    }

    public async Task<List<OfficialPost>> GetByClusterIdAsync(Guid clusterId, CancellationToken ct)
    {
        return await _db.OfficialPosts
            .Where(p => p.RelatedClusterId == clusterId && p.Status == "published")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<OfficialPost>> GetActiveByLocalityAsync(Guid localityId, CancellationToken ct)
    {
        // Returns published posts scoped to a locality, not yet expired
        var postIds = await _db.OfficialPostScopes
            .Where(s => s.LocalityId == localityId)
            .Select(s => s.OfficialPostId)
            .Distinct()
            .ToListAsync(ct);

        return await _db.OfficialPosts
            .Where(p => postIds.Contains(p.Id) && p.Status == "published")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<OfficialPost>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct)
    {
        var ids = localityIds?.ToList() ?? new List<Guid>();
        if (ids.Count == 0) return new List<OfficialPost>();

        var postIds = await _db.OfficialPostScopes
            .AsNoTracking()
            .Where(s => s.LocalityId != null && ids.Contains(s.LocalityId.Value))
            .Select(s => s.OfficialPostId)
            .Distinct()
            .ToListAsync(ct);

        // Short-circuit: no scoped posts means no results. Avoids a second
        // round-trip with an empty IN() set in the common case where a
        // followed locality set has no published official posts.
        if (postIds.Count == 0) return new List<OfficialPost>();

        return await _db.OfficialPosts
            .AsNoTracking()
            .Where(p => postIds.Contains(p.Id) && p.Status == "published")
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> ExpirePostsAsync(CancellationToken ct)
    {
        return await _db.OfficialPosts
            .Where(p => p.Status == "published" && p.EndsAt != null && p.EndsAt < DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, "expired")
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                ct);
    }
}

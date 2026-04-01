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

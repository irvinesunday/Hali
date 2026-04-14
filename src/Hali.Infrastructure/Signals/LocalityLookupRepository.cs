using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Signals;
using Hali.Infrastructure.Data.Signals;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Hali.Infrastructure.Signals;

public class LocalityLookupRepository : ILocalityLookupRepository
{
    private readonly SignalsDbContext _db;

    public LocalityLookupRepository(SignalsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<Guid, LocalitySummary>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new Dictionary<Guid, LocalitySummary>();

        var rows = await _db.Localities
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.WardName, l.CityName, l.CountyName })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => new LocalitySummary(r.Id, r.WardName, r.CityName, r.CountyName));
    }

    public async Task<LocalitySummary?> FindByPointAsync(
        double latitude,
        double longitude,
        CancellationToken ct = default)
    {
        var point = new Point(longitude, latitude) { SRID = 4326 };

        var match = await _db.Localities
            .Where(l => EF.Property<MultiPolygon>(l, "Geom").Contains(point))
            .Select(l => new { l.Id, l.WardName, l.CityName, l.CountyName })
            .FirstOrDefaultAsync(ct);

        return match is null
            ? null
            : new LocalitySummary(match.Id, match.WardName, match.CityName, match.CountyName);
    }

    public async Task<IReadOnlyList<LocalitySummary>> ListAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.Localities
            .OrderBy(l => l.WardName)
            .Select(l => new { l.Id, l.WardName, l.CityName, l.CountyName })
            .ToListAsync(ct);

        return rows
            .Select(r => new LocalitySummary(r.Id, r.WardName, r.CityName, r.CountyName))
            .ToList();
    }
}

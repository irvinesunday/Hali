using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Signals;

public record LocalitySummary(Guid Id, string WardName, string? CityName, string? CountyName);

public interface ILocalityLookupRepository
{
    Task<IReadOnlyDictionary<Guid, LocalitySummary>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// PostGIS point-in-polygon lookup. Returns the locality whose
    /// geom contains the supplied lat/lon, or null.
    /// </summary>
    Task<LocalitySummary?> FindByPointAsync(double latitude, double longitude, CancellationToken ct = default);
}

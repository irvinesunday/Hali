using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Signals;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Returns a deterministic locality so signal submission tests work without
/// requiring seeded locality geometry data in the test database.
/// </summary>
internal sealed class FakeLocalityLookupRepository : ILocalityLookupRepository
{
    public static readonly Guid TestLocalityId = Guid.Parse("deadbeef-0000-0000-0000-000000000001");

    private static readonly LocalitySummary TestLocality =
        new(TestLocalityId, "Test Ward", "Nairobi", "Nairobi");

    public Task<LocalitySummary?> FindByPointAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        return Task.FromResult<LocalitySummary?>(TestLocality);
    }

    public Task<IReadOnlyDictionary<Guid, LocalitySummary>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var result = ids
            .Where(id => id == TestLocalityId)
            .ToDictionary(id => id, _ => TestLocality);
        return Task.FromResult<IReadOnlyDictionary<Guid, LocalitySummary>>(result);
    }
}

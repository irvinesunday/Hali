using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Signals;

namespace Hali.Tests.Integration.Infrastructure;

/// <summary>
/// Returns a deterministic set of localities so signal submission tests work
/// without requiring seeded locality geometry data in the test database.
///
/// The fake holds multiple wards intentionally stored out of alphabetical
/// order so that callers exercising the contract (wards are returned sorted
/// ascending by ward name) can detect regressions. Only <see cref="TestLocalityId"/>
/// is inserted into the real test DB via IntegrationTestBase.SeedTestLocalityAsync —
/// the other rows exist only through this fake and are used only by callers
/// that do not take a DB-side FK on locality_id.
/// </summary>
internal sealed class FakeLocalityLookupRepository : ILocalityLookupRepository
{
    public static readonly Guid TestLocalityId = Guid.Parse("deadbeef-0000-0000-0000-000000000001");

    private static readonly LocalitySummary TestLocality =
        new(TestLocalityId, "Test Ward", "Nairobi", "Nairobi");

    // Extra wards — exercise ordering + the null city-name path without
    // requiring them to be seeded in the DB (they aren't FK targets).
    // Intentionally stored in non-alphabetical order; ListAllAsync sorts.
    private static readonly IReadOnlyList<LocalitySummary> AllWards = new[]
    {
        TestLocality,
        new LocalitySummary(Guid.Parse("deadbeef-0000-0000-0000-000000000002"), "Zulu Ward", "Nairobi", "Nairobi"),
        new LocalitySummary(Guid.Parse("deadbeef-0000-0000-0000-000000000003"), "Alpha Ward", null, null),
    };

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

    public Task<IReadOnlyList<LocalitySummary>> ListAllAsync(CancellationToken ct = default)
    {
        var sorted = AllWards
            .OrderBy(l => l.WardName, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<LocalitySummary>>(sorted);
    }
}
